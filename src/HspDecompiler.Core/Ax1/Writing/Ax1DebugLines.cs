using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using HspDecompiler.Core.Ax1.Data;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax1.Writing;

// The lines of a debug build. ASC writes a line marker after each line that has a statement, a
// label or a define, holding the line's number plus one, so the statements before a marker are
// its line's, joined with " : ", and the lines no marker names stay blank. Each if on a line
// jumps to the line's marker, unless an else on the line moved it to the else.
//
// デバッグビルドの行。ASC は文・ラベル・define のある各行の後に、行番号に 1 を足した値を持つ
// 行の印を書く。そのため印の前の文はその行のもので " : " でつなぎ、どの印も示さない行は空の
// ままにする。行の各 if は行の印へ飛ぶ。ただし行の else が飛び先を else に移した時は別である。
internal static class Ax1DebugLines
{
    internal static List<List<Ax1Part>?> Of(Ax1Context context, ImmutableSortedSet<int> labels)
    {
        Ax1Program program = context.Program;
        int[] numbers = Numbers(context);
        var markers = program
            .Lines.Select((t, j) => (t.Offset, j))
            .ToDictionary(p => p.Offset, p => p.j);
        ILookup<int, int> atMarker = labels.Where(markers.ContainsKey).ToLookup(l => markers[l]);
        List<int> rest = [];
        Dictionary<int, List<int>> before = Ax1Layout.LabelsBefore(
            context,
            labels.Where(l => !markers.ContainsKey(l)),
            rest
        );
        int total = (numbers.Length == 0 ? 0 : numbers[^1]) + 1;
        List<List<Ax1Part>?> lines = [.. Enumerable.Repeat<List<Ax1Part>?>(null, total)];
        foreach (int number in numbers)
        {
            lines[number - 1] = [];
        }

        int marker = 0;
        for (int i = 0; i < program.Statements.Length; i++)
        {
            Ax1Statement statement = program.Statements[i];
            while (marker < numbers.Length && program.Lines[marker].Offset < statement.End)
            {
                marker++;
            }

            List<Ax1Part> line = lines[
                (marker < numbers.Length ? numbers[marker] : total) - 1
            ] ??= [];
            line.AddRange(before.GetValueOrDefault(i, []).Select(l => new Ax1Part(null, l)));
            line.Add(new Ax1Part(statement, 0));
            CheckJumps(
                context,
                statement,
                marker < numbers.Length ? program.Lines[marker].Offset : -1
            );
        }

        for (int j = 0; j < numbers.Length; j++)
        {
            lines[numbers[j] - 1]!.AddRange(atMarker[j].Select(l => new Ax1Part(null, l)));
        }

        lines[total - 1] ??= rest.Count > 0 ? [] : null;
        lines[total - 1]?.AddRange(rest.Select(l => new Ax1Part(null, l)));
        return lines[total - 1] == null ? lines[..^1] : lines;
    }

    // The 1-based source line of each marker. A marker holds its line's number plus one; one
    // that does not follow the one before is warned about and takes the next line.
    //
    // 各印の 1 から数えるソースの行。印は行番号に 1 を足した値を持つ。前の印に続かない印は警告し、
    // 次の行を取る。
    private static int[] Numbers(Ax1Context context)
    {
        ImmutableArray<Ax1Token> markers = context.Program.Lines;
        int[] numbers = new int[markers.Length];
        int previous = 0;
        for (int j = 0; j < markers.Length; j++)
        {
            int number = markers[j].Value - 1;
            if (number <= previous)
            {
                context.Differs(markers[j].Offset, Strings.Ax1LineOrder);
                number = previous + 1;
            }

            numbers[j] = previous = number;
        }

        return numbers;
    }

    // An if jumps to its line's marker or to the closing 0x80 of an else, and an else's slot
    // holds the marker. A statement after the last marker has no marker to check against.
    //
    // if は行の印か else を閉じる 0x80 へ飛び、else のスロットは印を持つ。最後の印より後の文には
    // 照らし合わせる印が無い。
    private static void CheckJumps(Ax1Context context, Ax1Statement statement, int marker)
    {
        if (marker < 0 || statement.Tokens.IsEmpty)
        {
            return;
        }

        Ax1Token head = statement.Tokens[0];
        bool fits = statement.IsElse
            ? head.Value == marker
            : head.Kind != Ax1TokenKind.If
                || head.Value == marker
                || context.ElseEnds.Contains(head.Value);
        if (!fits)
        {
            context.Differs(statement.Offset, Strings.Ax1JumpTarget);
        }
    }
}
