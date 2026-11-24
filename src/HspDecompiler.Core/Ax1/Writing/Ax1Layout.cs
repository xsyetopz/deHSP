using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using HspDecompiler.Core.Ax1.Data;
using HspDecompiler.Core.Encoding;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax1.Writing;

// One part of an output line: a statement, or the definition of the label at an offset.
//
// 出力の行の 1 つの部分。文か、あるオフセットのラベルの定義。
internal readonly record struct Ax1Part(Ax1Statement? Statement, int Label);

// A string a statement wrote, where its inline literal starts in the statement's source, and
// the offset of its token.
//
// 文が書いた文字列と、文のソースでのその行内のリテラルの開始位置、およびそのトークンのオフセット。
internal sealed record Ax1LineString(Ax1Literal Literal, int Start, int Offset);

// One rendered source line and the continuation lines its last string needs after it.
//
// 書き出した 1 つのソースの行と、その最後の文字列が後に必要とする継続の行。
internal readonly record struct Ax1Line(
    string Text,
    ImmutableArray<string> Continuations,
    int Offset
);

// Lays the statements out on source lines and writes them. ASC writes a label as the offset
// where its definition stands, so a label goes before the statement at that offset, or, at a
// debug build's line marker, at the end of that marker's line.
//
// 文をソースの行に並べて書く。ASC はラベルを定義のあるオフセットとして書くため、ラベルはその
// オフセットの文の前に置く。デバッグビルドの行の印の位置なら、その印の行の末尾に置く。
internal static class Ax1Layout
{
    // The most bytes of a line ASC reads; a longer line overruns its buffer and stops it.
    //
    // ASC が読む行の最大のバイト数。より長い行はバッファーを溢れさせ、ASC を止める。
    internal const int MaxLine = 255;

    internal static List<string> Write(Ax1Context context)
    {
        Ax1Program program = context.Program;
        ImmutableSortedSet<int> labels = program.Unresolved ? [] : context.LabelTargets();
        List<List<Ax1Part>?> lines = program.IsDebug
            ? Ax1DebugLines.Of(context, labels)
            : Ax1ReleaseLines.Of(context, labels);
        var writer = new Ax1StatementWriter(context);
        var output = new List<string>(lines.Count);
        for (int n = 0; n < lines.Count; n++)
        {
            if (lines[n] is not { } parts)
            {
                output.Add("");
                continue;
            }

            Ax1Line line = Render(context, writer, parts, n + 1);
            output.Add(line.Text);
            n += Continue(context, lines, n, line, output);
        }

        return output;
    }

    // Writes the line's continuations and returns how many following lines they took. A debug
    // build marks every line, so there a continuation takes the place of a following line that
    // has a marker and nothing else.
    //
    // 行の継続を書き、それが占めた後続の行の数を返す。デバッグビルドはすべての行に印を付ける
    // ため、そこでは継続は、印だけを持つ後続の行の場所を取る。
    private static int Continue(
        Ax1Context context,
        List<List<Ax1Part>?> lines,
        int n,
        Ax1Line line,
        List<string> output
    )
    {
        int taken = 0;
        foreach (string continuation in line.Continuations)
        {
            if (context.Program.IsDebug)
            {
                bool free = n + 1 + taken < lines.Count && lines[n + 1 + taken] is { Count: 0 };
                taken += free ? 1 : 0;
                if (!free)
                {
                    context.Differs(line.Offset, Strings.Ax1ContinuationLines);
                }
            }

            output.Add("\t\t" + continuation + "+");
        }

        return taken;
    }

    // An empty part list is a debug line with a marker but no statement or label, such as a
    // define alone; a define keeps the marker.
    //
    // 空の部分の並びは、印はあるが文もラベルも無いデバッグの行(define だけの行など)である。
    // define が印を残す。
    private static Ax1Line Render(
        Ax1Context context,
        Ax1StatementWriter writer,
        List<Ax1Part> parts,
        int number
    )
    {
        var items = new List<string>();
        Ax1LineString? last = null;
        int lastItem = -1;
        foreach (Ax1Part part in parts)
        {
            if (part.Statement is not { } statement)
            {
                items.Add(context.Symbols.DefineAt(part.Label));
                continue;
            }

            var prefix = new List<string>();
            string body = writer.Write(statement, prefix);
            items.AddRange(prefix);
            if (writer.LastString is { } written)
            {
                writer.Abandon(last);
                (last, lastItem) = (written, items.Count);
            }

            if (body.Length > 0)
            {
                items.Add(body);
            }
        }

        if (items.Count == 0)
        {
            items.Add("define e" + number.ToString(CultureInfo.InvariantCulture) + " 0");
        }

        int offset = parts.Find(p => p.Statement != null).Statement?.Offset ?? 0;
        return Fit(
            context,
            items,
            last ?? new Ax1LineString(new Ax1Literal([""], true), 0, offset),
            lastItem
        );
    }

    // Joins the items into a line, and moves the last string into continuations when the line
    // is too long for ASC to read. A line without strings passes a last string with no pieces
    // to move, at the line's first statement, and lastItem -1.
    //
    // 項目を 1 行につなぎ、ASC が読めないほど行が長い時は最後の文字列を継続に移す。文字列の無い
    // 行は、移す部分の無い最後の文字列を行の最初の文の位置で渡し、lastItem を -1 にする。
    private static Ax1Line Fit(
        Ax1Context context,
        List<string> items,
        Ax1LineString last,
        int lastItem
    )
    {
        string text = Join(items);
        Ax1Literal literal = last.Literal;
        if (ShiftJisHelper.Encoding.GetByteCount(text) > MaxLine && lastItem >= 0)
        {
            literal = literal.Detached();
            string body = items[lastItem];
            items[lastItem] =
                body[..last.Start]
                + literal.Inline
                + body[(last.Start + last.Literal.Inline.Length)..];
            text = Join(items);
        }

        if (ShiftJisHelper.Encoding.GetByteCount(text) > MaxLine)
        {
            context.Differs(last.Offset, Strings.Ax1LineLength);
        }

        return new Ax1Line(text, literal.Continuations, last.Offset);
    }

    private static string Join(List<string> items)
    {
        string text = string.Join(" : ", items);
        return text.StartsWith('*') ? text : "\t" + text;
    }

    // Places each label before the statement at its offset, and a label at the program's end
    // in rest. A label at any other offset is warned about and goes before the next statement.
    //
    // 各ラベルをそのオフセットの文の前に置き、プログラムの終わりのラベルは rest に入れる。それ
    // 以外のオフセットのラベルは警告し、次の文の前に置く。
    internal static Dictionary<int, List<int>> LabelsBefore(
        Ax1Context context,
        IEnumerable<int> labels,
        List<int> rest
    )
    {
        ImmutableArray<Ax1Statement> statements = context.Program.Statements;
        var before = new Dictionary<int, List<int>>();
        foreach (int label in labels)
        {
            int next = 0;
            while (next < statements.Length && statements[next].Offset < label)
            {
                next++;
            }

            bool atEnd = next == statements.Length;
            if (atEnd ? label != context.Program.EndOffset : statements[next].Offset != label)
            {
                context.Differs(label, Strings.Ax1LabelOffset);
            }

            if (atEnd)
            {
                rest.Add(label);
                continue;
            }

            if (!before.TryGetValue(next, out List<int>? list))
            {
                before[next] = list = [];
            }

            list.Add(label);
        }

        return before;
    }
}
