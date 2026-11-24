using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using HspDecompiler.Core.Ax1.Data;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax1.Writing;

// The lines of a build without line markers (/r). An if jumps past the end of its line, so the
// statements from an if up to its target share one line, and an else's slot, the end of the
// line, extends it. Every other statement takes a line of its own, and a label at the start of
// a line takes a line of its own too.
//
// 行の印の無いビルド(/r)の行。if は行の終わりの先へ飛ぶため、if からその飛び先までの文は
// 1 行を共有し、else のスロット(行の終わり)はその行を延ばす。他の文はそれぞれ 1 行を取り、
// 行の始まりのラベルもそれ自身の行を取る。
internal static class Ax1ReleaseLines
{
    internal static List<List<Ax1Part>?> Of(Ax1Context context, ImmutableSortedSet<int> labels)
    {
        ImmutableArray<Ax1Statement> statements = context.Program.Statements;
        var rest = new List<int>();
        Dictionary<int, List<int>> before = Ax1Layout.LabelsBefore(context, labels, rest);
        var lines = new List<List<Ax1Part>?>();
        int i = 0;
        while (i < statements.Length)
        {
            int last = LastOfLine(context, i);
            lines.AddRange(
                before.GetValueOrDefault(i, []).Select(l => (List<Ax1Part>?)[new Ax1Part(null, l)])
            );
            var line = new List<Ax1Part> { new(statements[i], 0) };
            for (int j = i + 1; j <= last; j++)
            {
                line.AddRange(before.GetValueOrDefault(j, []).Select(l => new Ax1Part(null, l)));
                line.Add(new Ax1Part(statements[j], 0));
            }

            lines.Add(line);
            i = last + 1;
        }

        lines.AddRange(rest.Select(l => (List<Ax1Part>?)[new Ax1Part(null, l)]));
        return lines;
    }

    // The index of the last statement on the line that starts at statement first.
    //
    // 文 first で始まる行の最後の文の番号。
    private static int LastOfLine(Ax1Context context, int first)
    {
        ImmutableArray<Ax1Statement> statements = context.Program.Statements;
        int end = Jump(context, statements[first]);
        int last = first;
        while (last + 1 < statements.Length && statements[last + 1].Offset < end)
        {
            last++;
            end = Math.Max(end, Jump(context, statements[last]));
        }

        return last;
    }

    // Where an if or else statement jumps; a jump that lands on no statement start, program end
    // or else is warned about. Any other statement jumps nowhere, which ends no line early.
    //
    // if 文か else 文の飛び先。文の始まり・プログラムの終わり・else のどれにも当たらない飛び先は
    // 警告する。他の文はどこへも飛ばず、行を早く終えることもない。
    private static int Jump(Ax1Context context, Ax1Statement statement)
    {
        if (
            statement.Tokens.IsEmpty
            || (!statement.IsElse && statement.Tokens[0].Kind != Ax1TokenKind.If)
        )
        {
            return -1;
        }

        int target = statement.Tokens[0].Value;
        bool lands =
            target == context.Program.EndOffset
            || context.Program.Statements.Any(s => s.Offset == target)
            || (!statement.IsElse && context.ElseEnds.Contains(target));
        if (!lands)
        {
            context.Differs(statement.Offset, Strings.Ax1JumpTarget);
        }

        return target;
    }
}
