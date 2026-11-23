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

    // Matches the records and rebuilds the chosen blocks in the data, or returns null when no
    // layout fits them. keepsImports says optcode was off, so no module was dropped.
    //
    // 記録を照合し、選んだブロックをデータの中で組み直す。収まる配置が無ければ null を返す。
    // keepsImports は optcode が切られていたことを示し、その時はモジュールは削られていない。
    internal static MarkedLayout? Match(List<LogicalLine> lines, AxData data, bool keepsImports)
    {
        var headers = new List<LogicalLine>();
        var table = new RowTable(RowTable.Build(lines, headers));
        IReadOnlyList<int> marks = data.DebugInfo.Marks;
        (bool Lone, bool Unused)[] attempts = keepsImports
            ? [(false, true)]
            : [(true, true), (true, false), (false, true)];
        foreach ((bool lone, bool unused) in attempts)
        {
            var events = MarkEvents.Build(table, data, lone, unused);
            if (
                MarkMatcher.Match(table, events, marks) is { } choices
                && IsConsistent(events, choices)
            )
            {
                List<MarkedItem> items = MarkedModules.Apply(table, events, choices, marks, data);
                return new MarkedLayout(items, headers, data.Scope);
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
    internal SourceLayoutResult? Write(List<string> directives, string mainFile)
    {
        List<string> headers = [.. directives, .. _headers.Select(_scope.Render)];
        string stem = Path.GetFileNameWithoutExtension(mainFile);
        string extension = Path.GetExtension(mainFile);
        var placer = new MarkedPlacer(_scope, headers);
        List<List<string>>? parts = placer.Place(_items, part => $"{stem}_{part}{extension}");
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
        return new SourceLayoutResult(parts[0], includes);
    }
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
        for (int k = 0; k < choices.Length; k++)
        {
            lineOf[choices[k].Event] = marks[k];
            if (choices[k].NewPart)
            {
                parts.Add(events.Events[choices[k].Event].First);
            }
        }

        var items = new List<MarkedItem>();
        var skipped = new HashSet<int>();
        for (int row = 0; row < table.Rows.Count; row++)
        {
            int before = items.Count;
            if (!skipped.Contains(row))
            {
                AddRow(table, row, events, lineOf, data, items, skipped);
            }

            if (parts.Contains(row) && items.Count > before)
            {
                items[before].StartsPart = true;
            }
        }

        if (lineOf.Keys.Any(e => events.Events[e].Kind == MarkKind.Start))
        {
            Rescope(data);
        }

        return items;
    }

    private static void AddRow(
        RowTable table,
        int index,
        MarkEvents events,
        Dictionary<int, int> lineOf,
        AxData data,
        List<MarkedItem> items,
        HashSet<int> skipped
    )
    {
        var chosen = lineOf
            .Where(pair => events.Events[pair.Key].First == index)
            .Select(pair => (Event: events.Events[pair.Key], Line: pair.Value))
            .ToList();
        MarkedRow row = table.Rows[index];
        if (chosen.Find(c => c.Event.Kind == MarkKind.Start) is { Event: { } start } begin)
        {
            MarkBlock block = events.Blocks[start.Block];
            AddStart(table, block, begin.Line, lineOf, data, items);
            skipped.Add(block.Goto);
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

    private static void AddStart(
        RowTable table,
        MarkBlock block,
        int line,
        Dictionary<int, int> lineOf,
        AxData data,
        List<MarkedItem> items
    )
    {
        if (block.IsLone)
        {
            items.Add(
                new MarkedItem(new ModuleStart(string.Empty, true, null, -1), RowKind.Directive, [])
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
        var start = new ModuleStart(name, name.Length == 0, block.Members, startOffset);
        items.Add(new MarkedItem(start, RowKind.Directive, []) { Target = line });
        data.Scope.HasUnusedModule |= !block.Used;
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

    // A normal build records no label names, so every label is global; inside a module it
    // takes a bare "@".
    //
    // 通常ビルドはラベル名を記録しないため、すべてのラベルはグローバルであり、モジュールの
    // 中では "@" だけを付ける。
    private static void Rescope(AxData data)
    {
        ModuleBlocks.AddGlobalFunctions(data);
        foreach (Label label in data.Labels)
        {
            label.Owner = null;
            label.Scope = data.Scope;
        }
    }
}
