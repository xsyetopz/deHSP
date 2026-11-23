using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax3.Data.Line;
using HspDecompiler.Core.Ax3.Data.PP;

namespace HspDecompiler.Core.Ax3.Data.Layout;

// One output line of a normal build with the line it must stand on: a #module or #global on its
// record's line, or a string row whose {"..."} literals end on theirs.
//
// 通常ビルドの出力行と、それが立つべき行。#module・#global は記録の行に、文字列の行はその
// {"..."} リテラルが記録の行で終わるように置く。
internal sealed class MarkedItem(LogicalLine line, RowKind kind, int[] escapes)
{
    internal LogicalLine Line { get; } = line;

    internal RowKind Kind { get; } = kind;

    internal int[] Escapes { get; } = escapes;

    // The record line of a directive, or 0 for a line free to move.
    //
    // 指示文の記録の行。動かせる行では 0。
    internal int Target { get; init; }

    // The record line each {"..."} literal ends on, by its position among the "..." literals.
    //
    // 各 {"..."} リテラルが終わる記録の行("..." リテラル中の位置ごと)。
    internal SortedDictionary<int, int>? Ends { get; set; }

    internal bool StartsPart { get; set; }
}

// Rebuilds the line records hspcmp 3.1 to 3.32 write in a normal build. hspcmp records the line
// of each #module and #global (token.cpp PPRESULT_WROTE_LINES) and, from 3.3, the line each
// {"..."} string ends on, with no file names. So the records say where module blocks stood and
// which strings spanned lines. MarkMatcher gives each record its goto, label or literal, the
// blocks become #module and #global again, and MarkedPlacer puts each on its line.
//
// hspcmp 3.1〜3.32 が通常ビルドで書く行の記録を再現する。hspcmp は各 #module と #global の行
// (token.cpp の PPRESULT_WROTE_LINES)と、3.3 からは各 {"..."} 文字列が終わる行を、ファイル名
// 無しで記録する。そのため記録はモジュールブロックの位置と、複数行にわたった文字列を示す。
// MarkMatcher が各記録に goto・ラベル・リテラルを割り当て、ブロックを #module と #global に
// 戻し、MarkedPlacer がそれぞれを自分の行に置く。
internal sealed class MarkedLayout
{
    private readonly List<MarkedItem> _items;
    private readonly List<LogicalLine> _headers;
    private readonly ModuleScope _scope;

    private MarkedLayout(List<MarkedItem> items, List<LogicalLine> headers, ModuleScope scope)
    {
        _items = items;
        _headers = headers;
        _scope = scope;
    }

    internal bool HasUnusedModule =>
        _items.Exists(item => item.Line is ModuleStart { Used: false });

    // Matches the records and rebuilds the chosen blocks in the data, or returns null when no
    // layout fits them. keepsImports says optcode was off, so no module was dropped. Free
    // records, which a macro of the original source wrote, are tried only when no layout fits
    // without them, since they cost a line for the macro's definition.
    //
    // 記録を照合し、選んだブロックをデータの中で組み直す。収まる配置が無ければ null を返す。
    // keepsImports は optcode が切られていたことを示し、その時はモジュールは削られていない。
    // 元のソースのマクロが書いた自由な記録は、マクロの定義に 1 行を要するため、それ無しでは
    // 収まる配置が無い時だけ試す。
    internal static MarkedLayout? Match(List<LogicalLine> lines, AxData data, bool keepsImports)
    {
        var headers = new List<LogicalLine>();
        var table = new RowTable(RowTable.Build(lines, headers));
        IReadOnlyList<int> marks = data.DebugInfo.Marks;
        (bool Lone, bool Unused)[] attempts = keepsImports
            ? [(false, true)]
            : [(true, true), (true, false), (false, true)];
        foreach (bool free in (bool[])[false, true])
        {
            foreach ((bool lone, bool unused) in attempts)
            {
                var events = MarkEvents.Build(table, data, lone, unused);
                if (
                    MarkMatcher.Match(table, events, marks, free) is { } choices
                    && IsConsistent(events, choices)
                )
                {
                    List<MarkedItem> items = MarkedModules.Apply(
                        table,
                        events,
                        choices,
                        marks,
                        data
                    );
                    return new MarkedLayout(items, headers, data.Scope);
                }
            }
        }

        return null;
    }

    // A dropped module means optcode was on, and a block no code uses means it was off, so a
    // layout may hold one kind or the other.
    //
    // 削られたモジュールは optcode が有効だったことを、どのコードも使わないブロックは無効だった
    // ことを示すため、配置はどちらか一方しか持てない。
    private static bool IsConsistent(MarkEvents events, MarkChoice[] choices)
    {
        IEnumerable<MarkBlock> chosen = choices
            .Where(choice => !choice.Free)
            .Select(choice => events.Events[choice.Event])
            .Where(e => e.Kind == MarkKind.Start)
            .Select(e => events.Blocks[e.Block]);
        List<MarkBlock> blocks = [.. chosen];
        return !(blocks.Exists(b => b.IsLone) && blocks.Exists(b => !b.IsLone && !b.Used));
    }

    // Writes the source and the files its later parts went to, or returns null when the lines
    // do not fit the records after all.
    //
    // ソースと後の部分を入れたファイルを書く。結局行が記録に収まらなければ null を返す。
    internal DecodedSource? Write(List<string> directives, string mainFile)
    {
        List<string> headers = [.. directives, .. _headers.Select(_scope.Render)];
        string stem = Path.GetFileNameWithoutExtension(mainFile);
        string extension = Path.GetExtension(mainFile);
        string PartName(int part) => $"{stem}_{part}{extension}";
        List<List<string>>? parts = new MarkedPlacer(_scope, headers).Place(
            LabelsAfterGlobal(_items),
            PartName
        );
        if (parts == null)
        {
            _scope.Leave();
            parts = new MarkedPlacer(_scope, headers).Place(_items, PartName);
        }

        if (parts == null)
        {
            return null;
        }

        List<IncludeFile> includes =
        [
            .. parts
                .Skip(1)
                .Select((lines, i) => new IncludeFile($"{stem}_{i + 1}{extension}", lines)),
        ];
        return new DecodedSource(parts[0], includes);
    }

    // A label on the code a #global falls on goes after the #global when the records leave room:
    // inside the block it would read "*label_1@", after it the plain global label it is, and both
    // point at the same code.
    //
    // #global と同じコード位置のラベルは、記録に余地があれば #global の後に置く。ブロックの
    // 中では "*label_1@" と書く必要があるが、後ろなら本来のグローバルラベルのまま書け、どちらも
    // 同じコードを指す。
    private static List<MarkedItem> LabelsAfterGlobal(List<MarkedItem> items)
    {
        var result = new List<MarkedItem>(items.Count);
        foreach (MarkedItem item in items)
        {
            int first = result.Count;
            if (item.Line is EndOfModule end)
            {
                while (first > 0 && IsLabelAt(result[first - 1], end.ExitOffset))
                {
                    first--;
                }
            }

            List<MarkedItem> labels = result.GetRange(first, result.Count - first);
            result.RemoveRange(first, labels.Count);
            result.Add(item);
            result.AddRange(labels);
        }

        return result;
    }

    private static bool IsLabelAt(MarkedItem item, int offset) =>
        item is { Target: 0, StartsPart: false, Ends: null }
        && offset >= 0
        && item.Line is PreprocessorDeclaration { Declared: Label { Function: null } label }
        && label.TokenOffset == offset;
}

// Turns the chosen blocks back into #module and #global and lists the output lines with the
// record lines they must keep.
//
// 選んだブロックを #module と #global に戻し、出力行を守るべき記録の行と共に並べる。
internal static class MarkedModules
{
    internal static List<MarkedItem> Apply(
        RowTable table,
        MarkEvents events,
        MarkChoice[] choices,
        IReadOnlyList<int> marks,
        AxData data
    )
    {
        var lineOf = new Dictionary<int, int>();
        var parts = new HashSet<int>();
        var macros = new List<(int Row, int Line)>();
        for (int k = 0; k < choices.Length; k++)
        {
            if (choices[k].Free)
            {
                int after = choices[k].Event < 0 ? -1 : events.Events[choices[k].Event].Last;
                macros.Add((after, marks[k] - 1));
                continue;
            }

            lineOf[choices[k].Event] = marks[k];
            if (choices[k].NewPart)
            {
                parts.Add(events.Events[choices[k].Event].First);
            }
        }

        var items = new List<MarkedItem>();
        if (macros.Count > 0)
        {
            items.Add(MacroItem(LineRecordMacro.Definition(), 1));
            AddMacroUses(macros, -1, items);
        }

        var rebuild = new Rebuild(table, events, lineOf, data, items);
        for (int row = 0; row < table.Rows.Count; row++)
        {
            int before = items.Count;
            if (!rebuild.Skipped.Contains(row))
            {
                rebuild.AddRow(row);
            }

            if (parts.Contains(row) && items.Count > before)
            {
                items[before].StartsPart = true;
            }

            AddMacroUses(macros, row, items);
        }

        if (lineOf.Keys.Any(e => events.Events[e].Kind == MarkKind.Start))
        {
            Rescope(data, rebuild.Modules);
        }

        return items;
    }

    private static void AddMacroUses(
        List<(int Row, int Line)> macros,
        int row,
        List<MarkedItem> items
    )
    {
        foreach ((int _, int line) in macros.Where(m => m.Row == row))
        {
            items.Add(MacroItem(LineRecordMacro.Use(), line));
        }
    }

    private static MarkedItem MacroItem(LineRecordMacro line, int target) =>
        new(line, RowKind.Directive, []) { Target = target };

    // The rows being turned into items, with the module blocks rebuilt so far.
    //
    // 項目に変えている行と、これまでに組み直したモジュールのブロック。
    private sealed class Rebuild(
        RowTable table,
        MarkEvents events,
        Dictionary<int, int> lineOf,
        AxData data,
        List<MarkedItem> items
    )
    {
        internal HashSet<int> Skipped { get; } = [];

        internal List<ModuleRange> Modules { get; } = [];

        internal void AddRow(int index)
        {
            var chosen = lineOf
                .Where(pair => events.Events[pair.Key].First == index)
                .Select(pair => (Event: events.Events[pair.Key], Line: pair.Value))
                .ToList();
            MarkedRow row = table.Rows[index];
            if (chosen.Find(c => c.Event.Kind == MarkKind.Start) is { Event: { } start } begin)
            {
                MarkBlock block = events.Blocks[start.Block];
                AddStart(block, begin.Line);
                Skipped.Add(block.Goto);
                return;
            }

            if (chosen.Find(c => c.Event.Kind == MarkKind.End) is { Event: not null } end)
            {
                var exit = (Label)((PreprocessorDeclaration)row.Line).Declared!;
                items.Add(
                    new MarkedItem(new EndOfModule(exit.TokenOffset), RowKind.Directive, [])
                    {
                        Target = end.Line,
                    }
                );
                return;
            }

            var item = new MarkedItem(row.Line, row.Kind, row.Escapes);
            if (chosen.Count > 0)
            {
                item.Ends = new SortedDictionary<int, int>(
                    chosen.ToDictionary(c => c.Event.Ordinal, c => c.Line)
                );
            }

            items.Add(item);
        }

        private void AddStart(MarkBlock block, int line)
        {
            if (block.IsLone)
            {
                items.Add(
                    new MarkedItem(
                        new ModuleStart(string.Empty, true, null, -1),
                        RowKind.Directive,
                        []
                    )
                    {
                        Target = line,
                    }
                );
                items.Add(
                    new MarkedItem(new EndOfModule(-1), RowKind.Directive, [])
                    {
                        Target = lineOf[block.End],
                    }
                );
                return;
            }

            int startOffset = table.Rows[block.Goto].Line.TokenOffset;
            int exitOffset = (
                (Label)((PreprocessorDeclaration)table.Rows[block.LabelRow].Line).Declared!
            ).TokenOffset;
            string name = block.Members?.FunctionName
                ?? LocalModule(data, startOffset, exitOffset)
                ?? string.Empty;
            var start = new ModuleStart(name, name.Length == 0, block.Members, startOffset)
            {
                Used = block.Used,
            };
            items.Add(new MarkedItem(start, RowKind.Directive, []) { Target = line });
            Modules.Add(new ModuleRange(name, startOffset, exitOffset));
        }
    }

    // The code a rebuilt #module block holds, from its goto to its exit label.
    //
    // 組み直した #module ブロックが持つコード。goto から出口のラベルまで。
    private sealed record ModuleRange(string Name, int Start, int Exit)
    {
        internal bool Holds(int offset) => offset >= Start && offset < Exit;
    }

    // A "local" function keeps its module in its name as "name@module", which names the block
    // it lies in.
    //
    // "local" の関数は名前に "名前@モジュール" としてモジュールを残し、それがある
    // ブロックの名前を示す。
    private static string? LocalModule(AxData data, int startOffset, int exitOffset) =>
        data
            .Functions.Where(f =>
                f.IsUserFunction
                && f.Label is { TokenOffset: var offset }
                && offset >= startOffset
                && offset < exitOffset
            )
            .Select(f => f.FunctionName)
            .OfType<string>()
            .Where(name => name.Contains('@', StringComparison.Ordinal))
            .Select(name => name[(name.IndexOf('@', StringComparison.Ordinal) + 1)..])
            .FirstOrDefault();

    // A normal build records no label names, so the decompiler picks each label's module. A
    // label defined in a named block belongs to it, and code outside spells it "name@module". A
    // nameless block can't be named from outside, so a label there belongs to it only when every
    // reference is inside too; otherwise it is global and takes a bare "@" inside. Label names
    // leave no bytes in a normal build, so either choice compiles the same.
    //
    // 通常ビルドはラベル名を記録しないため、各ラベルのモジュールはデコンパイラが選ぶ。名前の
    // あるブロックで定義したラベルはそのブロックに属し、外のコードは "名前@モジュール" と書く。
    // 名前の無いブロックは外から名指せないため、すべての参照も中にある時だけそのブロックに属し、
    // それ以外はグローバルで、中では "@" だけを付ける。通常ビルドではラベル名はバイト列に
    // 残らないため、どちらを選んでも同じにコンパイルされる。
    private static void Rescope(AxData data, List<ModuleRange> modules)
    {
        ModuleBlocks.AddGlobalFunctions(data);
        foreach (Label label in data.Labels)
        {
            ModuleRange? home = modules.Find(m => m.Holds(label.TokenOffset));
            bool owned =
                home != null && (home.Name.Length > 0 || label.ReferenceOffsets.All(home.Holds));
            label.Owner = owned ? home!.Name : null;
            label.Scope = data.Scope;
        }
    }
}
