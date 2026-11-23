using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using HspDecompiler.Core.Abstractions;

namespace HspDecompiler.Core.Ax2.Data;

internal readonly record struct SourceLine(string Text, int Anchor);

internal readonly record struct FileSwitch(int Offset, string Name);

internal sealed record Ax2Source(List<string> Lines, List<IncludeFile> Includes);

// hspcmp 2.x appends literals, #func names and #deffunc names to the data segment in source
// order and never reuses an entry, so the first data offset a line introduces places it in the
// source. Each switch between files appends the file name there too: the included name on
// entry, the parent's name again on return. hspcmp includes hspdef.as before the main file by
// itself, so the first switch is the return into the main file.
//
// hspcmp 2.x はリテラル・#func 名・#deffunc 名をソース順にデータセグメントへ追加し、同じ
// エントリを再利用しない。そのため行が最初に持ち込むデータオフセットでソース上の位置が
// 決まる。ファイルが切り替わるたびにファイル名も追加される。入るときは取り込んだファイル名、
// 戻るときは親のファイル名である。hspcmp はメインファイルの前に hspdef.as を自動で取り込む
// ため、最初の切り替えはメインファイルへの戻りになる。
internal sealed class IncludeLayout
{
    private const string ImplicitHeader = "hspdef.as";

    private static readonly SearchValues<char> s_pathChars = SearchValues.Create("/\\:*?\"<>|");

    private readonly Segment _root;
    private readonly string? _mainFileName;
    private readonly List<IncludeFile> _includes = [];

    private IncludeLayout(Segment root, string? mainFileName)
    {
        _root = root;
        _mainFileName = mainFileName;
    }

    internal static bool IsSourceFileName(string text) =>
        text.EndsWith(".as", StringComparison.OrdinalIgnoreCase)
        || text.EndsWith(".hsp", StringComparison.OrdinalIgnoreCase);

    // Declarations go right after the last code line that precedes them in the data segment:
    // as early as the order allows, so each stays ahead of every call the source made after it.
    //
    // 宣言はデータセグメント上で直前にあるコード行のすぐ後ろに置く。順序が許す限り前に置く
    // ことで、ソースがその後に書いた呼び出しより必ず前に来る。
    internal static List<SourceLine> Merge(List<SourceLine> code, List<SourceLine> declarations)
    {
        var merged = new List<SourceLine>(code.Count + declarations.Count);
        int next = 0;
        int nextAnchor = NextAnchor(code, 0);
        AddDeclarationsBefore(merged, declarations, ref next, nextAnchor);
        for (int i = 0; i < code.Count; i++)
        {
            merged.Add(code[i]);
            if (code[i].Anchor < 0)
            {
                continue;
            }

            nextAnchor = NextAnchor(code, i + 1);
            AddDeclarationsBefore(merged, declarations, ref next, nextAnchor);
        }

        AddDeclarationsBefore(merged, declarations, ref next, int.MaxValue);
        return merged;
    }

    internal static Ax2Source Split(
        List<SourceLine> lines,
        List<FileSwitch> switches,
        string? mainFileName
    )
    {
        var layout = new IncludeLayout(BuildTree(switches), mainFileName);
        List<string> main = layout.Emit(layout._root, lines, 0, lines.Count);
        return new Ax2Source(main, layout._includes);
    }

    private static int NextAnchor(List<SourceLine> lines, int from)
    {
        for (int i = from; i < lines.Count; i++)
        {
            if (lines[i].Anchor >= 0)
            {
                return lines[i].Anchor;
            }
        }

        return int.MaxValue;
    }

    private static void AddDeclarationsBefore(
        List<SourceLine> merged,
        List<SourceLine> declarations,
        ref int next,
        int anchor
    )
    {
        while ((next < declarations.Count) && (declarations[next].Anchor < anchor))
        {
            merged.Add(declarations[next]);
            next++;
        }
    }

    private static Segment BuildTree(List<FileSwitch> switches)
    {
        var root = new Segment(null, null, 0);
        Segment current = root;
        foreach (FileSwitch fileSwitch in switches)
        {
            if ((current.Parent != null) && current.Parent.TakeName(fileSwitch.Name))
            {
                current.End = fileSwitch.Offset;
                current = current.Parent;
                continue;
            }

            var child = new Segment(fileSwitch.Name, current, fileSwitch.Offset);
            current.Children.Add(child);
            current = child;
        }

        return root;
    }

    private List<string> Emit(Segment segment, List<SourceLine> lines, int from, int to)
    {
        var output = new List<string>();
        int position = from;
        foreach (Segment child in segment.Children)
        {
            (int childFrom, int childTo) = FindRange(lines, position, to, child);
            output.AddRange(lines.Skip(position).Take(childFrom - position).Select(l => l.Text));
            List<string> body = Emit(child, lines, childFrom, childTo);
            if (IsImplicitHeader(child) && (body.Count == 0))
            {
                position = childTo;
                continue;
            }

            if (TryAddInclude(child.Name, body))
            {
                output.Add("#include \"" + child.Name + "\"");
            }
            else
            {
                output.AddRange(body);
            }

            position = childTo;
        }

        output.AddRange(lines.Skip(position).Take(to - position).Select(l => l.Text));
        return output;
    }

    // An include that left nothing in the data segment goes right after the last line before it.
    // Lines without data after an include's last anchored line, such as the return closing a
    // #deffunc, stay in the include up to the next label, since a parent usually jumps past an
    // include to a label.
    //
    // データセグメントに何も残さなかった取り込みは、その直前の行のすぐ後ろに置く。取り込みの
    // 最後のアンカー付き行に続くデータを持たない行(#deffunc を閉じる return など)は、次の
    // ラベルまで取り込み側に残す。親は取り込みを飛び越えてラベルへ移ることが多いためである。
    private static (int From, int To) FindRange(
        List<SourceLine> lines,
        int from,
        int to,
        Segment child
    )
    {
        int first = -1;
        int last = -1;
        int before = from - 1;
        for (int i = from; i < to; i++)
        {
            int anchor = lines[i].Anchor;
            if (anchor < 0)
            {
                continue;
            }

            if (anchor < child.Start)
            {
                before = i;
            }
            else if (anchor < child.End)
            {
                if (first < 0)
                {
                    first = i;
                }

                last = i;
            }
            else
            {
                break;
            }
        }

        if (first < 0)
        {
            return (before + 1, before + 1);
        }

        int end = last + 1;
        while ((end < to) && (lines[end].Anchor < 0) && !lines[end].Text.StartsWith('*'))
        {
            end++;
        }

        return (first, end);
    }

    private bool IsImplicitHeader(Segment child) =>
        (child.Parent == _root)
        && (_root.Children[0] == child)
        && string.Equals(child.Name, ImplicitHeader, StringComparison.OrdinalIgnoreCase);

    // Only a plain file name beside the main file is written; a path, a clash with the main file
    // or a second copy with other contents stays inline instead.
    //
    // 書き出すのはメインファイルと同じ場所の単純なファイル名だけである。パス、メインファイルと
    // の衝突、内容の異なる二度目の取り込みは展開したまま残す。
    private bool TryAddInclude(string? name, List<string> body)
    {
        if (
            string.IsNullOrEmpty(name)
            || name.StartsWith('.')
            || name.AsSpan().ContainsAny(s_pathChars)
            || name.Any(char.IsControl)
            || string.Equals(name, _mainFileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, ImplicitHeader, StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        IncludeFile? existing = _includes.Find(f =>
            string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)
        );
        if (existing != null)
        {
            return existing.Lines.SequenceEqual(body, StringComparer.Ordinal);
        }

        _includes.Add(new IncludeFile(name, body));
        return true;
    }

    private sealed class Segment
    {
        internal Segment(string? name, Segment? parent, int start)
        {
            Name = name;
            Parent = parent;
            Start = start;
        }

        internal string? Name { get; private set; }

        internal Segment? Parent { get; }

        internal int Start { get; }

        internal int End { get; set; } = int.MaxValue;

        internal List<Segment> Children { get; } = [];

        // The main file's name first appears on the return from hspdef.as.
        //
        // メインファイルの名前は hspdef.as からの戻りで初めて現れる。
        internal bool TakeName(string name)
        {
            if (Name == null)
            {
                Name = name;
                return true;
            }

            return string.Equals(Name, name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
