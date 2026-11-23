using System;
using System.Collections.Generic;
using System.Linq;

namespace HspDecompiler.Core.Ax3.Data.Layout;

// The event that wrote one record, and whether its row starts a new include file. A free record
// came from no event: a line record macro writes it right after the rows of Event, which may be
// -1 for the start of the source.
//
// 1 つの記録を書いた位置と、その行が新しい取り込みファイルを始めるかどうか。自由な記録はどの
// 位置からも来ていない。行の記録マクロが Event の行の直後でそれを書く。Event はソースの先頭を
// 表す -1 のこともある。
internal readonly record struct MarkChoice(int Event, bool NewPart, bool Free);

// Gives each line record of a normal build the event that wrote it. The records come in source
// order, so the events they take keep that order, a #module takes the #global of its own block
// next, and each event has to fit on its line: the rows since the last one need that many lines
// at least, with blank rows dropped and code joined. A record whose line is too small for that
// came from an included file, which counts its lines from 1 again, so the rows from its event
// on start a new file; that costs more than any choice of events, so it happens only when no
// layout of one file fits. Of the layouts that fit, the one with the lowest cost wins.
//
// With free records on, a record may also come from no event, as a "%c" macro of the original
// source wrote it (LineRecordMacro). The macro use takes the line before the record on its own,
// after the rows of the last event and before the rest, and its definition takes line 1, so the
// first event starts on line 2 at the earliest. Such a record costs more than any string, and
// less than a new file.
//
// 通常ビルドの各行の記録に、それを書いた位置を割り当てる。記録はソース順に並ぶため、割り当てる
// 位置もその順を守り、#module の次は同じブロックの #global を取る。各位置は自分の行に収まる
// 必要がある。前の位置からの行は、空行を落としコードをつないでも少なくともその行数を要する。
// その行に収まらない記録は、行を 1 から数え直す取り込みファイルから来たため、その位置からの
// 行は新しいファイルを始める。これはどの位置の選び方よりも費用が高く、1 つのファイルでは
// 収まらない時だけ起きる。収まる配置の中で費用が最も低いものを選ぶ。
//
// 自由な記録を許すと、記録はどの位置からも来ないこともできる。元のソースの "%c" マクロが書いた
// 記録である(LineRecordMacro)。マクロの使用は記録の前の行を単独で占め、最後の位置の行の後、
// 残りの行の前に置く。定義は 1 行目を占めるため、最初の位置は早くとも 2 行目に来る。そうした
// 記録はどの文字列よりも高く、新しいファイルよりは安い。
internal sealed class MarkMatcher
{
    private const int PartCost = 1000;
    private const int FreeCost = 100;

    private readonly RowTable _table;
    private readonly List<MarkEvent> _events;
    private readonly List<MarkBlock> _blocks;
    private readonly bool _free;

    private MarkMatcher(RowTable table, MarkEvents events, bool free)
    {
        _table = table;
        _events = events.Events;
        _blocks = events.Blocks;
        _free = free;
    }

    internal static MarkChoice[]? Match(
        RowTable table,
        MarkEvents events,
        IReadOnlyList<int> marks,
        bool free
    ) => new MarkMatcher(table, events, free).Run(marks);

    private MarkChoice[]? Run(IReadOnlyList<int> marks)
    {
        var layers = new List<Layer>();
        var previous = new Layer();
        previous.Relax(-1, -1, 0, -1, false, false);
        int line = _free ? 1 : 0;
        foreach (int mark in marks)
        {
            var layer = new Layer();
            for (int i = 0; i < previous.Nodes.Count; i++)
            {
                Direct(previous.Nodes[i], i, line, mark, layer);
                Free(previous.Nodes[i], i, line, mark, layer);
            }

            Sweep(previous.Nodes, line, mark, layer);
            if (layer.Nodes.Count == 0)
            {
                return null;
            }

            layers.Add(layer);
            previous = layer;
            line = mark;
        }

        return Trace(layers);
    }

    private static MarkChoice[]? Trace(List<Layer> layers)
    {
        List<Node> last = layers[^1].Nodes;
        int best = -1;
        for (int i = 0; i < last.Count; i++)
        {
            if (last[i].Open < 0 && (best < 0 || last[i].Cost < last[best].Cost))
            {
                best = i;
            }
        }

        if (best < 0)
        {
            return null;
        }

        var choices = new MarkChoice[layers.Count];
        for (int k = layers.Count - 1; k >= 0; k--)
        {
            Node node = layers[k].Nodes[best];
            choices[k] = new MarkChoice(node.Event, node.NewPart, node.Free);
            best = node.Previous;
        }

        return choices;
    }

    // A free record from a node: its macro use stands on the line before the record, after the
    // node's own line, and the rows after the node follow the use.
    //
    // ノードからの自由な記録。マクロの使用は記録の前の行、ノード自身の行より後に立ち、ノードの
    // 後の行はその使用の後に続く。
    private void Free(Node node, int index, int line, int mark, Layer layer)
    {
        if (_free && mark - 1 > LineOf(node, line))
        {
            layer.Relax(node.Event, node.Open, node.Cost + FreeCost, index, false, true);
        }
    }

    // Targets a sweep does not cover: the rest of an open block, the strings later in the same
    // row, and the events on the next filled row, which may join the line of the last one.
    //
    // 一括の走査が扱わない行き先。開いたブロックの残り、同じ行の後の文字列、そして前の位置の
    // 行につながりうる次の空でない行の位置である。
    private void Direct(Node node, int index, int line, int mark, Layer layer)
    {
        line = LineOf(node, line);
        if (node.Open >= 0)
        {
            int end = _blocks[node.Open].End;
            for (int t = node.Event + 1; t <= end; t++)
            {
                if ((_events[t].Kind == MarkKind.Text || t == end) && Fits(node, t, line, mark))
                {
                    int open = t == end ? -1 : node.Open;
                    layer.Relax(t, open, node.Cost + _events[t].Cost, index, false, false);
                }
            }

            return;
        }

        int lastRow = LastRow(node);
        int filled = _table.NextFilled(lastRow + 1);
        for (int t = node.Event + 1; t < _events.Count && _events[t].First <= filled; t++)
        {
            MarkEvent target = _events[t];
            if (target.Kind != MarkKind.End && Fits(node, t, line, mark))
            {
                layer.Relax(t, OpenAfter(target), node.Cost + target.Cost, index, false, false);
            }
        }
    }

    // Every other target of a node without an open block: a target on a row past the next filled
    // one fits when its packed line is no later than the record, so one pass over the targets in
    // row order, with the nodes ranked by how far they reach, finds the cheapest node for each.
    //
    // 開いたブロックの無いノードのそれ以外の行き先。次の空でない行より後の行き先は、詰めた行が
    // 記録より後でなければ収まる。そこで行き先を行の順に 1 度たどり、ノードを届く範囲で並べて、
    // それぞれに最も安いノードを見つける。
    private void Sweep(List<Node> nodes, int line, int mark, Layer layer)
    {
        List<int> free = [.. Enumerable.Range(0, nodes.Count).Where(i => nodes[i].Open < 0)];
        int[] reach = new int[nodes.Count];
        foreach (int i in free)
        {
            int start = LastRow(nodes[i]) + 1;
            int filled = _table.NextFilled(start);
            reach[i] =
                mark - LineOf(nodes[i], line) + _table.Before(start) - Correction(nodes[i], filled);
        }

        int[] byFilled = [.. free.OrderBy(i => _table.NextFilled(LastRow(nodes[i]) + 1))];
        int[] byLast = [.. free.OrderBy(i => LastRow(nodes[i]))];
        int[] values = [.. free.Select(i => reach[i]).Distinct().OrderByDescending(v => v)];
        var ranked = new MinTree(values.Length);
        int filledAt = 0;
        int lastAt = 0;
        int part = -1;
        for (int t = 0; t < _events.Count; t++)
        {
            MarkEvent target = _events[t];
            for (
                ;
                filledAt < byFilled.Length && FilledRow(nodes[byFilled[filledAt]]) < target.First;
                filledAt++
            )
            {
                int i = byFilled[filledAt];
                ranked.Add(Array.BinarySearch(values, reach[i], s_descending), nodes[i].Cost, i);
            }

            for (
                ;
                lastAt < byLast.Length && LastRow(nodes[byLast[lastAt]]) < target.First;
                lastAt++
            )
            {
                int i = byLast[lastAt];
                part = part < 0 || nodes[i].Cost < nodes[part].Cost ? i : part;
            }

            if (target.Kind != MarkKind.End)
            {
                int key = _table.Before(target.First) + Anchor(target);
                Reach(t, ranked.Min(CountAtLeast(values, key)), nodes, layer, false);
                Reach(t, part, nodes, layer, true);
            }
        }
    }

    private static readonly IComparer<int> s_descending = Comparer<int>.Create(
        (a, b) => b.CompareTo(a)
    );

    private static int CountAtLeast(int[] descending, int key)
    {
        int low = 0;
        int high = descending.Length;
        while (low < high)
        {
            int middle = (low + high) / 2;
            if (descending[middle] >= key)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private void Reach(int target, int node, List<Node> nodes, Layer layer, bool newPart)
    {
        if (node < 0)
        {
            return;
        }

        int cost = nodes[node].Cost + _events[target].Cost + (newPart ? PartCost : 0);
        layer.Relax(target, OpenAfter(_events[target]), cost, node, newPart, false);
    }

    private bool Fits(Node node, int target, int line, int mark)
    {
        MarkEvent next = _events[target];
        int lastRow = LastRow(node);
        if (next.First <= lastRow)
        {
            return !node.Free && next.Kind == MarkKind.Text
                ? mark >= line && mark - line <= next.Escapes
                : next.Kind == MarkKind.End && mark > line;
        }

        int start = lastRow + 1;
        int filled = _table.NextFilled(start);
        if (next.First <= filled)
        {
            return mark >= line + (next.IsDirective || !Joinable(node) ? 1 : 0);
        }

        int packed = _table.Before(next.First) - _table.Before(start) + Correction(node, filled);
        return mark >= line + packed + Anchor(next);
    }

    private static int OpenAfter(MarkEvent target) =>
        target.Kind == MarkKind.Start ? target.Block : -1;

    // A directive takes a line of its own; a string row may join the line before it.
    //
    // 指示文は自分の行を占め、文字列の行は前の行につながりうる。
    private int Anchor(MarkEvent target) => target.IsDirective ? 1 : _table.Width(target.First);

    // The first filled row after a node counts by what really comes before it, a directive or a
    // string row, rather than by the row the event replaced.
    //
    // ノードの後の最初の空でない行は、置き換えた行ではなく、実際に前に来るもの(指示文か文字列の
    // 行)で数える。
    private int Correction(Node node, int filled)
    {
        if (filled >= _table.Rows.Count)
        {
            return 0;
        }

        int width = _table.Rows[filled].TakesJoin && Joinable(node) ? 0 : 1;
        return width - _table.Width(filled);
    }

    private bool Joinable(Node node) =>
        !node.Free && node.Event >= 0 && _events[node.Event].Kind == MarkKind.Text;

    // The line after which a node's rows follow: its record's line, or for a free record the
    // line of its macro use before it.
    //
    // ノードの行が続く直前の行。その記録の行、自由な記録ではその前のマクロの使用の行。
    private static int LineOf(Node node, int line) => node.Free ? line - 1 : line;

    private int LastRow(Node node) => node.Event < 0 ? -1 : _events[node.Event].Last;

    private int FilledRow(Node node) => _table.NextFilled(LastRow(node) + 1);

    private sealed record Node(
        int Event,
        int Open,
        int Cost,
        int Previous,
        bool NewPart,
        bool Free
    );

    private sealed class Layer
    {
        private readonly Dictionary<(int Event, int Open, bool Free), int> _index = [];

        internal List<Node> Nodes { get; } = [];

        internal void Relax(int target, int open, int cost, int previous, bool newPart, bool free)
        {
            if (_index.TryGetValue((target, open, free), out int at))
            {
                if (cost < Nodes[at].Cost)
                {
                    Nodes[at] = new Node(target, open, cost, previous, newPart, free);
                }

                return;
            }

            _index[(target, open, free)] = Nodes.Count;
            Nodes.Add(new Node(target, open, cost, previous, newPart, free));
        }
    }

    // Prefix minimum of node costs over ranks, for the nodes added so far.
    //
    // これまでに加えたノードの費用の、順位に対する前方最小値。
    private sealed class MinTree(int size)
    {
        private readonly int[] _cost = [.. Enumerable.Repeat(int.MaxValue, size + 1)];
        private readonly int[] _node = new int[size + 1];

        internal void Add(int rank, int cost, int node)
        {
            for (int i = rank + 1; i <= size; i += i & -i)
            {
                if (cost < _cost[i])
                {
                    _cost[i] = cost;
                    _node[i] = node;
                }
            }
        }

        // The cheapest node among the first count ranks, or -1 for none.
        //
        // 先頭から count 個の順位の中で最も安いノード。無ければ -1。
        internal int Min(int count)
        {
            int best = int.MaxValue;
            int node = -1;
            for (int i = count; i > 0; i -= i & -i)
            {
                if (_cost[i] < best)
                {
                    best = _cost[i];
                    node = _node[i];
                }
            }

            return node;
        }
    }
}
