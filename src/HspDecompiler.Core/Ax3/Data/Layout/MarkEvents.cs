using System.Collections.Generic;
using System.Linq;
using HspDecompiler.Core.Ax3.Data.Line;
using HspDecompiler.Core.Ax3.Data.PP;

namespace HspDecompiler.Core.Ax3.Data.Layout;

internal enum MarkKind
{
    Start,
    End,
    Text,
}

// A place in the rows that may have written one line record: the #module or #global of a block,
// or one "..." literal written as {"..."}. First and Last are the rows it takes.
//
// 行の中で行の記録を 1 つ書いた可能性のある位置。ブロックの #module か #global、または
// {"..."} と書いた 1 つの "..." リテラル。First と Last はそれが占める行である。
internal sealed record MarkEvent(
    MarkKind Kind,
    int First,
    int Last,
    int Block,
    int Ordinal,
    int Escapes
)
{
    internal bool IsDirective => Kind != MarkKind.Text;

    // A block costs nothing, so a record goes to a module when it can. A string costs one, and
    // one more when it holds no line break, since a one-line {"..."} is the rarer spelling.
    //
    // ブロックは費用 0 のため、記録はできる限りモジュールに割り当てる。文字列は費用 1 で、
    // 1 行の {"..."} は稀な書き方のため、改行を持たなければさらに 1 を足す。
    internal int Cost =>
        Kind != MarkKind.Text ? 0
        : Escapes > 0 ? 1
        : 2;
}

// A goto and the label it jumps to that may be the "#module" and "#global" hspcmp expanded, or a
// label alone left by a module hspcmp dropped.
//
// hspcmp が展開した "#module" と "#global" である可能性のある goto とその飛び先のラベル、
// または hspcmp が削ったモジュールが残したラベル単体。
internal sealed record MarkBlock(
    int End,
    int First,
    int Goto,
    int LabelRow,
    Function? Members,
    bool Used
)
{
    internal bool IsLone => Goto < 0;
}

internal sealed class MarkEvents
{
    private MarkEvents(List<MarkEvent> events, List<MarkBlock> blocks)
    {
        Events = events;
        Blocks = blocks;
    }

    internal List<MarkEvent> Events { get; }

    internal List<MarkBlock> Blocks { get; }

    // withLone admits labels alone as dropped modules, which only optcode leaves behind;
    // withUnused admits goto blocks no code uses, which only a build without optcode keeps.
    //
    // withLone は単独のラベルを削られたモジュールとして認める。それを残すのは optcode だけで
    // ある。withUnused はどのコードも使わない goto ブロックを認める。それを残すのは optcode の
    // 無いビルドだけである。
    internal static MarkEvents Build(RowTable table, AxData data, bool withLone, bool withUnused)
    {
        List<MarkedRow> rows = table.Rows;
        var starts = new Dictionary<int, MarkBlock>();
        var ends = new Dictionary<int, MarkBlock>();
        for (int i = 0; i < rows.Count; i++)
        {
            if (
                rows[i].Line
                is not PreprocessorDeclaration { Declared: Label { Function: null } exit }
            )
            {
                continue;
            }

            MarkBlock? block =
                exit.References == 0 && withLone ? new MarkBlock(0, i, -1, i, null, false)
                : exit.References == 1 ? GotoBlock(rows, i, exit, data)
                : null;
            if (block != null && (withUnused || block.IsLone || block.Used))
            {
                starts[block.First] = block;
                ends[i] = block;
            }
        }

        return Collect(rows, starts, ends);
    }

    private static MarkEvents Collect(
        List<MarkedRow> rows,
        Dictionary<int, MarkBlock> starts,
        Dictionary<int, MarkBlock> ends
    )
    {
        var events = new List<MarkEvent>();
        var blocks = new List<MarkBlock>();
        for (int i = 0; i < rows.Count; i++)
        {
            if (starts.TryGetValue(i, out MarkBlock? block))
            {
                int last = block.IsLone ? i : block.Goto;
                events.Add(new MarkEvent(MarkKind.Start, i, last, blocks.Count, 0, 0));
                blocks.Add(block);
            }

            if (ends.TryGetValue(i, out MarkBlock? closing))
            {
                int index = blocks.FindIndex(b => b.LabelRow == closing.LabelRow);
                events.Add(new MarkEvent(MarkKind.End, i, i, index, 0, 0));
                blocks[index] = blocks[index] with { End = events.Count - 1 };
            }

            int[] escapes = rows[i].Escapes;
            for (int ordinal = 0; ordinal < escapes.Length; ordinal++)
            {
                events.Add(new MarkEvent(MarkKind.Text, i, i, -1, ordinal, escapes[ordinal]));
            }
        }

        return new MarkEvents(events, blocks);
    }

    // hspcmp writes "goto *_name_exit" for #module, so the goto is the only use of the label,
    // and a module declares neither a struct nor a #runtime inside itself. A module with
    // methods needs the #struct line right before the goto, which names it.
    //
    // hspcmp は #module に "goto *_名前_exit" を書くため、goto はそのラベルの唯一の使用であり、
    // モジュールは中で struct も #runtime も宣言しない。メソッドを持つモジュールには、名前を示す
    // #struct 行が goto の直前に要る。
    private static MarkBlock? GotoBlock(List<MarkedRow> rows, int labelRow, Label exit, AxData data)
    {
        string jump = "goto " + exit.LabelName;
        int start = rows.FindLastIndex(labelRow, labelRow + 1, row =>
            row.Line is Command && row.Line.ToString() == jump
        );
        if (start < 0)
        {
            return null;
        }

        IEnumerable<Preprocessor?> inside = rows.Skip(start + 1)
            .Take(labelRow - start - 1)
            .Select(row => (row.Line as PreprocessorDeclaration)?.Declared);
        bool methods = false;
        foreach (Preprocessor? declared in inside)
        {
            if (declared is Runtime or Function { IsModuleFunction: true })
            {
                return null;
            }

            methods |= declared is Function { IsMethod: true };
        }

        Function? members =
            start > 0
            && rows[start - 1].Line
                is PreprocessorDeclaration { Declared: Function { IsModuleFunction: true } module }
                ? module
                : null;
        if (methods && members == null)
        {
            return null;
        }

        int first = members != null ? start - 1 : start;
        bool used = ModuleBlocks.IsUsed(
            data,
            members,
            rows[start].Line.TokenOffset,
            exit.TokenOffset
        );
        return new MarkBlock(0, first, start, labelRow, members, used);
    }
}
