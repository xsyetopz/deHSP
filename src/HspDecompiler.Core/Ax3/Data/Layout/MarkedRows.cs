using System.Collections.Generic;

namespace HspDecompiler.Core.Ax3.Data.Layout;

internal sealed class MarkedRow(LogicalLine line, LineKind kind, int[] escapes)
{
    internal LogicalLine Line { get; } = line;

    internal LineKind Kind { get; } = kind;

    // The "\n" escapes of each "..." literal of a code row, in order.
    //
    // コード行の各 "..." リテラルが持つ "\n" エスケープの数(順序どおり)。
    internal int[] Escapes { get; } = escapes;

    internal bool TakesJoin => Kind.TakesJoin();

    internal bool LetsJoin => Kind.LetsJoin();
}

// The rows of a normal build and the fewest source lines each stretch of them needs: blank rows
// drop and each code row or brace joins the line before it where it may.
//
// 通常ビルドの行と、その区間が必要とする最少のソース行数。空行は落とし、コード行と括弧は
// 許される所で前の行につなぐ。
internal sealed class RowTable
{
    private readonly int[] _lines;
    private readonly int[] _next;

    internal RowTable(List<MarkedRow> rows)
    {
        Rows = rows;
        _lines = new int[rows.Count + 1];
        _next = new int[rows.Count + 1];
        bool letsJoin = false;
        for (int i = 0; i < rows.Count; i++)
        {
            MarkedRow row = rows[i];
            int width =
                row.Kind == LineKind.Blank ? 0
                : row.TakesJoin && letsJoin ? 0
                : 1;
            _lines[i + 1] = _lines[i] + width;
            letsJoin = row.Kind == LineKind.Blank ? letsJoin : row.LetsJoin;
        }

        _next[rows.Count] = rows.Count;
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            _next[i] = rows[i].Kind == LineKind.Blank ? _next[i + 1] : i;
        }
    }

    internal List<MarkedRow> Rows { get; }

    // Packed lines of the rows before the given one.
    //
    // 指定した行より前の行を詰めた時の行数。
    internal int Before(int row) => _lines[row];

    internal int Width(int row) => _lines[row + 1] - _lines[row];

    // The first row at or after the given one that is not blank, or the row count for none.
    //
    // 指定した行以降で最初の空行でない行。無ければ行数。
    internal int NextFilled(int row) => _next[row];

    internal static List<MarkedRow> Build(List<LogicalLine> lines, List<LogicalLine> headers)
    {
        var rows = new List<MarkedRow>();
        foreach (LogicalLine line in lines)
        {
            if (!line.Visible)
            {
                continue;
            }

            LineKind kind = LineKinds.Of(line);
            if (kind == LineKind.Header)
            {
                headers.Add(line);
                continue;
            }

            int[] escapes = kind == LineKind.Code ? LongStrings.Escapes(line.ToString()) : [];
            rows.Add(new MarkedRow(line, kind, escapes));
        }

        return rows;
    }
}
