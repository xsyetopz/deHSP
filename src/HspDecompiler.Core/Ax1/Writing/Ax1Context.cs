using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using HspDecompiler.Core.Ax1.Data;
using HspDecompiler.Core.Keywords;
using HspDecompiler.Core.Releases;

namespace HspDecompiler.Core.Ax1.Writing;

// What every part of one HSP 1 decode reads: the program, the keyword names of its release, the
// symbol table and the offsets the loops and labels occupy.
//
// 1 回の HSP 1 の復元のすべての部分が読むもの。プログラム、そのリリースのキーワードの名前、
// 記号表、ループとラベルが占めるオフセット。
internal sealed record Ax1Context(
    Ax1Program Program,
    KeywordDictionary Dictionary,
    ReleaseProfile Release,
    Ax1Symbols Symbols,
    Action<int, string> Differs
)
{
    internal const int Goto = 0xb1;
    internal const int Repeat = 0xb8;
    internal const int Loop = 0xba;

    // The offset of each loop's closing 0x80, which a break jumps to.
    //
    // 各 loop を閉じる 0x80 のオフセット。break はそこへ飛ぶ。
    internal ImmutableHashSet<int> LoopEnds { get; } =
    [.. Program.Statements.Where(IsLoop).Select(s => s.End - 1)];

    // Where each repeat statement ends, which its loop jumps back to.
    //
    // 各 repeat 文の終わり。対応する loop はそこへ戻る。
    internal ImmutableHashSet<int> RepeatEnds { get; } =
    [.. Program.Statements.Where(s => s.StartsWith(Repeat)).Select(s => s.End)];

    // The offset of each else's closing 0x80, where ASC moves the if before the else to.
    //
    // 各 else を閉じる 0x80 のオフセット。ASC は else の前の if の飛び先をそこへ移す。
    internal ImmutableHashSet<int> ElseEnds { get; } =
    [.. Program.Statements.Where(s => s.IsElse).Select(s => s.End - 1)];

    internal static bool IsLoop(Ax1Statement s) =>
        s.Tokens.Length == 3
        && s.StartsWith(Loop)
        && s.Tokens[1].IsByte(Goto)
        && s.Tokens[2].Kind == Ax1TokenKind.LabelOffset;

    internal bool IsBreak(Ax1Statement s, int i) =>
        s.Tokens[i].IsByte(Goto)
        && i + 1 < s.Tokens.Length
        && s.Tokens[i + 1].Kind == Ax1TokenKind.LabelOffset
        && LoopEnds.Contains(s.Tokens[i + 1].Value);

    // The offsets a goto, gosub, button or other label argument names; the jumps of if, else,
    // loop and break are not labels.
    //
    // goto・gosub・button などのラベルの引数が指すオフセット。if・else・loop・break の飛び先は
    // ラベルではない。
    internal ImmutableSortedSet<int> LabelTargets() =>
        [
            .. Program
                .Statements.Where(s => !s.IsElse && !IsLoop(s))
                .SelectMany(s =>
                    s.Tokens.Select((t, i) => (t, i))
                        .Where(p =>
                            p.t.Kind == Ax1TokenKind.LabelOffset
                            && !(p.i > 0 && IsBreak(s, p.i - 1))
                        )
                        .Select(p => p.t.Value)
                ),
        ];

    internal string Keyword(int type, int value) =>
        Dictionary.NameOf(type, value, Release) ?? value.ToString(CultureInfo.InvariantCulture);
}
