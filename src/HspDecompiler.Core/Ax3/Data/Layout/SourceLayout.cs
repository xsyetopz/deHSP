using System.Collections.Generic;
using System.Linq;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax3.Data.Line;
using HspDecompiler.Core.Ax3.Data.PP;

namespace HspDecompiler.Core.Ax3.Data.Layout;

internal sealed record SourceLayoutResult(List<string> Main, List<IncludeFile> Includes, int Moved);

// Rebuilds the files and line layout of a debug build from its line table. Each code line goes
// to the file and line whose code range holds its first token; every other line goes to the file
// of the code after it. Each expanded #include goes back on its line of the parent, and each
// file gets as many lines as hspcmp counted, so a recompile in debug mode writes the same table.
// A file that held code is written beside the main source. hspdef.as is included by hspcmp
// itself and is never written. A file of the common directory without code (hspcmp.as, a header
// of #define lines) left only expanded definitions behind, so its #include is kept as a comment
// unless the caller found the file and dropped the imports it declares.
//
// デバッグビルドの行テーブルからファイルと行の配置を組み直す。コード行は先頭トークンを含む
// コード範囲のファイルと行へ、それ以外の行は後に続くコードのファイルへ置く。展開された
// #include は親の元の行へ戻し、各ファイルは hspcmp が数えた行数にするため、デバッグモードで
// 再コンパイルすると同じテーブルになる。コードを持つファイルはメインのソースの隣に書き出す。
// hspdef.as は hspcmp 自身が取り込むため書き出さない。コードの無い共通ディレクトリのファイル
// (hspcmp.as や #define だけのヘッダー)は展開済みの定義しか残さないため、呼び出し側が
// ファイルを見つけて宣言済みのインポートを除いた場合を除き、その #include はコメントにする。
internal static class SourceLayout
{
    internal static SourceLayoutResult Build(
        List<LogicalLine> lines,
        List<string> header,
        AxData data,
        IncludeNames names,
        ISet<int> realCommonIncludes
    )
    {
        DebugInfo info = data.DebugInfo;
        List<LayoutItem> items = Classify(lines, header, data, names.MainFile);
        var byFile = items
            .GroupBy(item => item.File)
            .ToDictionary(group => group.Key, group => group.ToList());
        foreach (DebugInclude include in info.Includes)
        {
            if (!byFile.TryGetValue(include.Parent, out List<LayoutItem>? parent))
            {
                parent = [];
                byFile[include.Parent] = parent;
            }

            InsertInclude(
                parent,
                IncludeItem(include, names, byFile, realCommonIncludes),
                FirstCode(info, include.Child)
            );
        }

        var main = new List<string>();
        var includes = new List<IncludeFile>();
        int moved = 0;
        foreach ((int file, List<LayoutItem> fileItems) in byFile.OrderBy(entry => entry.Key))
        {
            if (file == names.HeaderFile || IsCommonWithoutCode(file, names, byFile))
            {
                continue;
            }

            var placer = new FilePlacer();
            List<string> placed = placer.Place(fileItems, LineCount(info, file, names.MainFile));
            moved += placer.Moved;
            if (file == names.MainFile)
            {
                main = placed;
            }
            else
            {
                includes.Add(new IncludeFile(names.OutputName(file), placed));
            }
        }

        return new SourceLayoutResult(main, includes, moved);
    }

    private static List<LayoutItem> Classify(
        List<LogicalLine> lines,
        List<string> header,
        AxData data,
        int mainFile
    )
    {
        List<LayoutItem> items =
        [
            .. header.Select(text => new LayoutItem(LayoutKind.Header, text, 0, mainFile, 0)),
        ];
        int first = items.Count;
        ModuleScope scope = data.Scope;
        List<DebugLine> code = [.. data.DebugInfo.Lines.Where(record => record.CodeSize > 0)];
        var breaks = new Dictionary<int, Queue<int>>();
        foreach (LogicalLine line in lines.Where(line => line.Visible))
        {
            items.Add(Classify(line, scope, code, breaks));
        }

        AssignFiles(items, first, mainFile);
        return items;
    }

    // A #module keeps the line of the goto hspcmp expanded it to, which DebugInfo already put
    // back on the #module line, but it never joins other code: a directive takes a line of its own.
    //
    // #module は hspcmp が展開した goto の行を使う。その行は DebugInfo が既に #module の行へ
    // 戻している。ただし指示文は 1 行を占めるため、他のコードとはつながない。
    private static LayoutItem Classify(
        LogicalLine line,
        ModuleScope scope,
        List<DebugLine> code,
        Dictionary<int, Queue<int>> breaks
    )
    {
        string text = scope.Render(line);
        if (line.TokenOffset >= 0 && Find(code, line.TokenOffset) is DebugLine record)
        {
            if (!breaks.TryGetValue(record.CodeOffset, out Queue<int>? pending))
            {
                pending = LongStrings.Breaks(record);
                breaks[record.CodeOffset] = pending;
            }

            text = LongStrings.Render(text, pending);
            return line is ModuleStart
                ? new LayoutItem(LayoutKind.ModuleStart, text, 0, record.File, record.Line)
                : new LayoutItem(LayoutKind.Code, text, line.TabCount, record.File, record.Line);
        }

        if (line is EndOfModule end)
        {
            return new LayoutItem(LayoutKind.ModuleEnd, text, 0, -1, 0)
            {
                CodeOffset = end.ExitOffset,
            };
        }

        LayoutKind kind = line switch
        {
            ScopeEnd => LayoutKind.Close,
            PreprocessorDeclaration { Declared: Runtime } => LayoutKind.Header,
            PreprocessorDeclaration { Declared: Label { Function: null } } => LayoutKind.Label,
            CommentLine when text.Length == 0 => LayoutKind.Blank,
            _ => LayoutKind.Other,
        };
        return new LayoutItem(kind, text, line.TabCount, -1, 0);
    }

    private static DebugLine? Find(List<DebugLine> code, int offset)
    {
        int low = 0;
        int high = code.Count - 1;
        while (low <= high)
        {
            int middle = (low + high) / 2;
            DebugLine record = code[middle];
            if (offset < record.CodeOffset)
            {
                high = middle - 1;
            }
            else if (offset >= record.CodeOffset + record.CodeSize)
            {
                low = middle + 1;
            }
            else
            {
                return record;
            }
        }

        return null;
    }

    // A line without code belongs with the code after it; lines after the last code stay with it.
    // A #global closes the module its #module opened, so it stays in that file.
    //
    // コードの無い行は後に続くコードと同じファイルに属し、最後のコードより後の行はそのコードに従う。
    // #global は #module が開いたモジュールを閉じるため、そのファイルに残る。
    private static void AssignFiles(List<LayoutItem> items, int first, int mainFile)
    {
        int module = -1;
        for (int i = first; i < items.Count; i++)
        {
            if (items[i].Kind == LayoutKind.ModuleStart)
            {
                module = items[i].File;
            }
            else if (items[i].Kind == LayoutKind.ModuleEnd)
            {
                items[i].File = module;
            }
        }

        int next = -1;
        for (int i = items.Count - 1; i >= first; i--)
        {
            if (items[i].File >= 0)
            {
                next = items[i].File;
            }
            else if (next >= 0)
            {
                items[i].File = next;
            }
        }

        int previous = mainFile;
        for (int i = first; i < items.Count; i++)
        {
            if (items[i].File >= 0)
            {
                previous = items[i].File;
            }
            else
            {
                items[i].File = previous;
            }
        }
    }

    private static LayoutItem IncludeItem(
        DebugInclude include,
        IncludeNames names,
        Dictionary<int, List<LayoutItem>> byFile,
        ISet<int> realCommonIncludes
    )
    {
        string text = "#include \"" + names.IncludeText(include.Child) + "\"";
        if (IsCommentedCommon(include.Child, names, byFile, realCommonIncludes))
        {
            text = "; " + text;
        }

        return new LayoutItem(LayoutKind.Include, text, 0, include.Parent, include.Line);
    }

    // The include goes after the last code line before it and any braces that close there. A
    // #global there goes before it when the module closed ahead of the included code.
    //
    // #include はその前にある最後のコード行と、そこで閉じる括弧の後ろに入れる。そこにある
    // #global は、取り込んだコードより前でモジュールが閉じていれば #include の前に置く。
    private static void InsertInclude(List<LayoutItem> items, LayoutItem include, int childCode)
    {
        int index = items.FindLastIndex(item => item.IsAnchor && item.Target < include.Target) + 1;
        while (
            index < items.Count
            && (
                items[index].Kind == LayoutKind.Close
                || (
                    items[index].Kind == LayoutKind.ModuleEnd
                    && items[index].CodeOffset <= childCode
                )
            )
        )
        {
            index++;
        }

        if (index == 0)
        {
            index = items.FindIndex(item => item.Kind != LayoutKind.Header);
            index = index < 0 ? items.Count : index;
        }

        items.Insert(index, include);
    }

    // The first code offset of a file or of the files it includes, or int.MaxValue for none.
    //
    // ファイルまたはそれが取り込むファイルの最初のコード位置。コードが無ければ int.MaxValue。
    private static int FirstCode(DebugInfo info, int file)
    {
        var files = new HashSet<int> { file };
        foreach (DebugInclude include in info.Includes)
        {
            if (files.Contains(include.Parent))
            {
                files.Add(include.Child);
            }
        }

        return info
            .Lines.Where(record => record.CodeSize > 0 && files.Contains(record.File))
            .Select(record => record.CodeOffset)
            .DefaultIfEmpty(int.MaxValue)
            .Min();
    }

    private static bool IsCommentedCommon(
        int file,
        IncludeNames names,
        Dictionary<int, List<LayoutItem>> byFile,
        ISet<int> realCommonIncludes
    ) => !realCommonIncludes.Contains(file) && IsCommonWithoutCode(file, names, byFile);

    // hspcmp reads such a file from the common directory, so it is never written.
    //
    // hspcmp はこのようなファイルを共通ディレクトリから読むため、書き出さない。
    private static bool IsCommonWithoutCode(
        int file,
        IncludeNames names,
        Dictionary<int, List<LayoutItem>> byFile
    ) =>
        names.IsCommon(file)
        && !(
            byFile.TryGetValue(file, out List<LayoutItem>? items)
            && items.Any(item => item.Kind == LayoutKind.Code)
        );

    // hspcmp counts one more line in the main file for the end code it appends after the source.
    //
    // hspcmp はソースの後ろに付け足す終了コードの分、メインファイルの行を 1 つ多く数える。
    private static int LineCount(DebugInfo info, int file, int mainFile)
    {
        int last = info
            .Lines.Where(record => record.File == file)
            .Select(record => record.Line)
            .DefaultIfEmpty(0)
            .Max();
        return file == mainFile ? last - 1 : last;
    }
}
