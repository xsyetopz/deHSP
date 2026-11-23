using System.Collections.Generic;
using System.Linq;

namespace HspDecompiler.Core.Ax3.Data.Layout;

// Writes an if or else block that fills the rest of its source line as "if x : y" instead of
// "if x { y }". hspcmp ends a colon if at the end of its line, so both compile to the same
// bytes and the same line table. A block is written that way only when its open, every statement
// of its body and the opens of its nested blocks stand on one line, nothing but closing braces
// follows it there, its close stands on that line or on a later line of closing braces alone,
// every block inside it and its if or else partner qualify too, and no multi-line string runs
// through the line. Anything else keeps its braces, because hspcmp has not been checked to
// compile it the same way.
//
// ソース行の残りを占める if・else ブロックを "if x { y }" ではなく "if x : y" と書く。hspcmp は
// コロンの if を行末で閉じるため、どちらも同じバイト列と行テーブルになる。そう書くのは、
// ブロックの開始・本体のすべての文・入れ子のブロックの開始が 1 行にあり、その行で後に続くのが
// 閉じ括弧だけで、閉じ括弧がその行か閉じ括弧だけの後の行にあり、内側のブロックと if・else の
// 相手も条件を満たし、複数行の文字列がその行を通らない時に限る。それ以外は、hspcmp が同じに
// コンパイルすることを確かめていないため括弧を残す。
internal static class ColonIfs
{
    private sealed class Block
    {
        internal int Open { get; set; } = -1;

        internal int Close { get; set; } = -1;

        internal int Partner { get; set; } = -1;

        internal int Line { get; set; } = -1;

        internal int CloseLine { get; set; } = -1;
    }

    internal static void Apply(
        List<string> lines,
        IReadOnlyList<List<LayoutItem>> members,
        IReadOnlySet<int> split,
        List<LayoutItem> order
    )
    {
        var lineOf = new Dictionary<LayoutItem, int>(ReferenceEqualityComparer.Instance);
        for (int line = 0; line < members.Count; line++)
        {
            foreach (LayoutItem item in members[line])
            {
                lineOf[item] = line;
            }
        }

        Dictionary<int, Block> blocks = Find(order, lineOf);
        var colon = blocks
            .Where(entry => Fits(entry.Value, order, lineOf, members, split))
            .Select(entry => entry.Key)
            .ToHashSet();
        while (colon.RemoveWhere(id => !Holds(id, blocks, colon, order, members)) > 0) { }

        var rewritten = colon.SelectMany(id => Lines(blocks[id])).Distinct().Order().ToList();
        foreach (int line in rewritten)
        {
            string text = lines[line];
            string joined = Rewrite(members[line], colon);
            lines[line] = joined.Length == 0 ? joined : text[..Tabs(text)] + joined;
        }

        foreach (int line in rewritten.Where(line => lines[line].Length == 0))
        {
            Unjoin(lines, members, split, line + 1, colon);
        }
    }

    // A close that the placer gave a line of its own may have pushed a label onto the statement
    // after it ("*main : mes"). Once the close is gone, the label takes the freed line back.
    //
    // 配置で 1 行を占めた閉じ括弧が、ラベルを後ろの文へ押しやった("*main : mes")ことがある。
    // 閉じ括弧が消えたら、ラベルは空いた行に戻る。
    private static void Unjoin(
        List<string> lines,
        IReadOnlyList<List<LayoutItem>> members,
        IReadOnlySet<int> split,
        int line,
        HashSet<int> colon
    )
    {
        if (
            line < lines.Count
            && !split.Contains(line)
            && members[line] is [{ Kind: LayoutKind.Label } label, { } anchor, ..]
        )
        {
            lines[line - 1] = new string('\t', label.Tabs) + label.Text;
            lines[line] = new string('\t', anchor.Tabs) + Rewrite(members[line].Skip(1), colon);
        }
    }

    private static int Tabs(string text) => text.Length - text.TrimStart('\t').Length;

    private static Dictionary<int, Block> Find(
        List<LayoutItem> order,
        Dictionary<LayoutItem, int> lineOf
    )
    {
        var blocks = new Dictionary<int, Block>();
        for (int i = 0; i < order.Count; i++)
        {
            LayoutItem item = order[i];
            int id = item.Opens >= 0 ? item.Opens : item.Closes;
            if (id < 0 || !lineOf.TryGetValue(item, out int line))
            {
                continue;
            }

            if (!blocks.TryGetValue(id, out Block? block))
            {
                block = new Block();
                blocks[id] = block;
            }

            if (item.Opens >= 0)
            {
                (block.Open, block.Line) = (i, line);
                if (item.Continues >= 0 && blocks.TryGetValue(item.Continues, out Block? partner))
                {
                    block.Partner = item.Continues;
                    partner.Partner = id;
                }
            }
            else
            {
                (block.Close, block.CloseLine) = (i, line);
            }
        }

        return blocks;
    }

    // Conditions on the block alone: its body is code on its open line, and its close stands
    // there or on a later line of closing braces after nothing but such lines.
    //
    // ブロック単独の条件。本体が開始行のコードで、閉じ括弧がその行か、閉じ括弧だけの行のみを
    // 挟んだ後の閉じ括弧だけの行にある。
    private static bool Fits(
        Block block,
        List<LayoutItem> order,
        Dictionary<LayoutItem, int> lineOf,
        IReadOnlyList<List<LayoutItem>> members,
        IReadOnlySet<int> split
    )
    {
        if (block.Open < 0 || block.Close <= block.Open + 1 || split.Contains(block.Line))
        {
            return false;
        }

        bool code = false;
        for (int i = block.Open + 1; i < block.Close; i++)
        {
            LayoutItem item = order[i];
            if (
                !lineOf.TryGetValue(item, out int line)
                || item.Kind is not (LayoutKind.Code or LayoutKind.Close)
            )
            {
                return false;
            }

            code |= item.Kind == LayoutKind.Code;
            if (line != block.Line && !ClosesOnly(members[line]))
            {
                return false;
            }
        }

        return code
            && Enumerable
                .Range(block.Line + 1, block.CloseLine - block.Line)
                .All(line => ClosesOnly(members[line]) && !split.Contains(line));
    }

    private static bool ClosesOnly(List<LayoutItem> items) =>
        items.Count > 0 && items.All(item => item.Kind == LayoutKind.Close);

    // Conditions that depend on other blocks: the partner qualifies, every block inside
    // qualifies, and on the open line only closes of qualifying blocks follow the chain.
    //
    // 他のブロックに依る条件。相手が条件を満たし、内側のブロックもすべて満たし、開始行で連なりの
    // 後に続くのは条件を満たすブロックの閉じ括弧だけである。
    private static bool Holds(
        int id,
        Dictionary<int, Block> blocks,
        HashSet<int> colon,
        List<LayoutItem> order,
        IReadOnlyList<List<LayoutItem>> members
    )
    {
        Block block = blocks[id];
        if (block.Partner >= 0 && !colon.Contains(block.Partner))
        {
            return false;
        }

        for (int i = block.Open + 1; i < block.Close; i++)
        {
            if (order[i].Opens >= 0 && !colon.Contains(order[i].Opens))
            {
                return false;
            }
        }

        if (block.Partner >= 0 && !Paired(block, blocks[block.Partner]))
        {
            return false;
        }

        Block last = block.Partner > id ? blocks[block.Partner] : block;
        if (last.CloseLine != block.Line)
        {
            return true;
        }

        List<LayoutItem> line = members[block.Line];
        return line.Skip(line.IndexOf(order[last.Close]) + 1)
            .All(item => item.Closes >= 0 && colon.Contains(item.Closes));
    }

    // An if and its else share the open line, and the if closes on it before the else opens.
    //
    // if とその else は開始行を共有し、else が開く前に if がその行で閉じる。
    private static bool Paired(Block block, Block partner)
    {
        Block first = partner.Open < block.Open ? partner : block;
        return partner.Line == block.Line && first.CloseLine == block.Line;
    }

    private static IEnumerable<int> Lines(Block block) =>
        block.CloseLine == block.Line ? [block.Line] : [block.Line, block.CloseLine];

    private static string Rewrite(IEnumerable<LayoutItem> items, HashSet<int> colon)
    {
        string? joined = null;
        foreach (LayoutItem item in items)
        {
            if (item.Closes >= 0 && colon.Contains(item.Closes))
            {
                continue;
            }

            string part =
                item.Opens >= 0
                && colon.Contains(item.Opens)
                && item.Text.EndsWith(" {", System.StringComparison.Ordinal)
                    ? item.Text[..^2]
                    : item.Text;
            joined = joined == null ? part : FilePlacer.Join(joined, part);
        }

        return joined ?? string.Empty;
    }
}
