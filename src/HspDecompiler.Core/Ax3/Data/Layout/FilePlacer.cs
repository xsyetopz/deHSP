using System.Collections.Generic;
using System.Linq;

namespace HspDecompiler.Core.Ax3.Data.Layout;

// Puts the lines of one source file back on the lines the debug segment recorded. Each code
// line and #include lands on its line; statements that shared a source line join with " : ",
// and the lines without code fill the gaps between them. A line without code compiles to no
// code, so where it sits changes nothing but readability, as long as the code keeps its line.
// When the gap is too small, blank lines drop first, then braces and a label join the code next
// to them ("} else {", "*main : mes"), and only then does the code move down a line. A header
// directive (#bootopt, #runtime, #cmpopt) acts on the whole compile wherever it stands, so it
// takes the first gap with room for it rather than pushing code down.
//
// 1 つのソースファイルの行を、デバッグセグメントが記録した行へ戻す。コード行と #include は
// その行に置き、同じソース行にあった文は " : " でつなぎ、コードの無い行はその間を埋める。
// コードの無い行はコードを生まないため、コードが行を保つ限りどこにあっても読みやすさしか
// 変わらない。間が足りない時は、まず空行を落とし、次に括弧とラベルを隣のコードにつなぎ
// ("} else {"、"*main : mes")、それでも足りない時だけコードを 1 行下げる。ヘッダーの指定
// (#bootopt・#runtime・#cmpopt)はどこにあってもコンパイル全体に効くため、コードを下げずに
// 入る余地のある最初の隙間に置く。
internal sealed class FilePlacer
{
    private readonly List<string> _lines = [];
    private readonly List<LayoutItem> _pending = [];
    private readonly Queue<LayoutItem> _headers = [];
    private readonly List<List<LayoutItem>> _members = [];
    private readonly HashSet<int> _split = [];
    private bool _lastTakesCode;

    // Lines the last written item took past its first, for a {"..."} string that spans lines.
    //
    // 最後に書いた項目が最初の行より後に使った行数。複数行にわたる {"..."} 文字列の分である。
    private int _extraLines;

    // Anchors that could not keep their line.
    //
    // 行を保てなかったアンカーの数。
    internal int Moved { get; private set; }

    // The items on each written line, in order. A line that a multi-line string runs through is
    // listed in Split.
    //
    // 書いた各行の項目(順序どおり)。複数行の文字列が通る行は Split に入る。
    internal IReadOnlyList<List<LayoutItem>> Members => _members;

    internal IReadOnlySet<int> Split => _split;

    internal List<string> Place(IEnumerable<LayoutItem> items, int lineCount)
    {
        foreach (LayoutItem item in items)
        {
            if (item.IsAnchor)
            {
                Flush(item);
            }
            else if (item.Kind == LayoutKind.Header)
            {
                _headers.Enqueue(item);
            }
            else
            {
                _pending.Add(item);
            }
        }

        foreach (LayoutItem item in _headers.Concat(_pending))
        {
            WriteLine(item);
        }

        _headers.Clear();
        _pending.Clear();
        while (_lines.Count < lineCount)
        {
            AddBlank();
        }

        return _lines;
    }

    private void Flush(LayoutItem anchor)
    {
        int free = anchor.Target - 1 - _lines.Count;
        if (free < 0)
        {
            FlushOnCurrentLine(anchor);
        }
        else
        {
            FlushBefore(anchor, free);
        }

        _pending.Clear();
    }

    // The anchor belongs on the line already written: the next statement of the same line.
    //
    // アンカーが既に書いた行に属する。同じ行の次の文である。
    private void FlushOnCurrentLine(LayoutItem anchor)
    {
        bool sameLine =
            anchor.Target == _lines.Count - _extraLines && anchor.Kind == LayoutKind.Code;
        if (
            sameLine
            && _lastTakesCode
            && _pending.All(p => p.Kind is LayoutKind.Close or LayoutKind.Blank)
        )
        {
            foreach (LayoutItem close in _pending.Where(p => p.Kind == LayoutKind.Close))
            {
                Append(close);
            }

            Append(anchor);
            return;
        }

        WriteMoved(_pending, 0, anchor);
    }

    private void FlushBefore(LayoutItem anchor, int free)
    {
        var items = new List<LayoutItem>(_pending);
        int room = free - items.Count(p => p.Kind != LayoutKind.Blank);
        for (int i = 0; i < room && _headers.Count > 0; i++)
        {
            items.Insert(i, _headers.Dequeue());
        }

        if (items.Count > free)
        {
            items.RemoveAll(p => p.Kind == LayoutKind.Blank);
        }

        LayoutItem? prefix = null;
        if (items.Count > free && anchor.Kind == LayoutKind.Code && JoinsNext(items, anchor))
        {
            prefix = items[^1];
            items.RemoveAt(items.Count - 1);
        }

        int joined = 0;
        if (items.Count > free && _lastTakesCode)
        {
            while (
                joined < items.Count
                && items[joined].Kind == LayoutKind.Close
                && items.Count - joined > free
            )
            {
                joined++;
            }
        }

        for (int i = 0; i < joined; i++)
        {
            Append(items[i]);
        }

        if (items.Count - joined > free)
        {
            WriteMoved(items, joined, Prefixed(prefix, anchor));
            return;
        }

        WriteSpread(items, joined, anchor.Target);
        WriteAnchor(Prefixed(prefix, anchor));
    }

    // A closing brace joins an else after it, and a label joins the statement it names.
    //
    // 閉じ括弧は後ろの else に、ラベルはそれが指す文につなげる。
    private static bool JoinsNext(List<LayoutItem> items, LayoutItem anchor) =>
        items.Count > 0
        && (
            items[^1].Kind == LayoutKind.Label
            || (
                items[^1].Kind == LayoutKind.Close
                && anchor.Text.StartsWith("else", System.StringComparison.Ordinal)
            )
        );

    private static LayoutItem Prefixed(LayoutItem? prefix, LayoutItem anchor) =>
        prefix == null
            ? anchor
            : new LayoutItem(
                anchor.Kind,
                Join(prefix.Text, anchor.Text),
                prefix.Tabs,
                anchor.File,
                anchor.Target
            )
            {
                Parts = [prefix, anchor],
            };

    // Early lines go right after the written ones, the others right before the anchor.
    //
    // 前寄せの行は書いた行の直後に、それ以外はアンカーの直前に置く。
    private void WriteSpread(List<LayoutItem> items, int from, int target)
    {
        int early = from;
        while (early < items.Count && items[early].IsEarly)
        {
            WriteLine(items[early]);
            early++;
        }

        int late = items.Count - early;
        while (_lines.Count < target - 1 - late)
        {
            AddBlank();
        }

        for (int i = early; i < items.Count; i++)
        {
            WriteLine(items[i]);
        }
    }

    private void WriteMoved(List<LayoutItem> items, int from, LayoutItem anchor)
    {
        for (int i = from; i < items.Count; i++)
        {
            WriteLine(items[i]);
        }

        WriteAnchor(anchor);
        Moved++;
    }

    private void WriteAnchor(LayoutItem anchor)
    {
        while (_lines.Count < anchor.Target - 1)
        {
            AddBlank();
        }

        WriteLine(anchor);
    }

    private void WriteLine(LayoutItem item)
    {
        string[] parts = (new string('\t', item.Tabs) + item.Text).Split('\n');
        _members.Add([.. item.Parts ?? [item]]);
        AddLines(parts, 0);
        _extraLines = parts.Length - 1;
        _lastTakesCode = item.Kind is LayoutKind.Code or LayoutKind.Close;
    }

    private void Append(LayoutItem item)
    {
        string[] parts = Join(_lines[^1], item.Text).Split('\n');
        _lines[^1] = parts[0];
        _members[^1].AddRange(item.Parts ?? [item]);
        AddLines(parts, 1);
        _extraLines += parts.Length - 1;
        _lastTakesCode = true;
    }

    private void AddBlank()
    {
        _lines.Add(string.Empty);
        _members.Add([]);
        _extraLines = 0;
        _lastTakesCode = false;
    }

    private void AddLines(string[] parts, int from)
    {
        if (parts.Length > 1)
        {
            _split.UnionWith(Enumerable.Range(_lines.Count - from, parts.Length));
        }

        for (int i = from; i < parts.Length; i++)
        {
            _lines.Add(parts[i]);
            if (i > 0)
            {
                _members.Add([]);
            }
        }
    }

    // hspcmp reads "{" and "}" as statements of their own, so they need no ':' beside them.
    //
    // hspcmp は "{" と "}" をそれ自体で文として読むため、その隣に ':' は要らない。
    internal static string Join(string first, string second) =>
        first.EndsWith('{')
        || second.StartsWith('}')
        || (first.EndsWith('}') && second.StartsWith("else", System.StringComparison.Ordinal))
            ? first + " " + second
            : first + " : " + second;
}
