using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using HspDecompiler.Core.Encoding;

namespace HspDecompiler.Core.Ax1.Writing;

// A stored string as source literals: the one on the statement's line, then the continuations.
// A line whose first token is a string directly followed by "+" appends that string to the one
// ASC stored last and writes no code, so each continuation goes on a line of its own as
// "literal"+.
//
// 格納された文字列をソースのリテラルとして表したもの。文の行に置くものと、その継続。最初の
// トークンが直後に "+" の続く文字列である行は、その文字列を ASC が最後に格納した文字列に足し、
// コードを書かない。そのため各継続は "リテラル"+ として自身の行に置く。
internal sealed record Ax1Literal(ImmutableArray<string> Pieces, bool Exact)
{
    internal const string Empty = "\"\"";

    internal string Inline => Pieces[0];

    internal ImmutableArray<string> Continuations => Pieces[1..];

    // The same bytes with the whole string in continuations, for a line too long to hold it.
    //
    // 同じバイト列で、文字列全体を継続に置いたもの。文字列を収められないほど長い行のため。
    internal Ax1Literal Detached() =>
        Inline == Empty ? this : this with { Pieces = [Empty, .. Pieces] };
}

// Spells a stored string as the source literals ASC turns back into it. ASC reads the source
// byte by byte with no Shift_JIS awareness: "\n" and "\N" become CR LF, "\t" and "\T" a tab,
// and a backslash before any other byte keeps both bytes. A kept backslash and quote also end
// the literal, so a stored "\"" ends a piece, which then has no closing quote of its own, and
// the rest of the string goes on in a continuation.
//
// 格納された文字列を、ASC が元に戻すソースのリテラルとして書く。ASC は Shift_JIS を意識せず
// ソースを 1 バイトずつ読む。"\n" と "\N" は CR LF、"\t" と "\T" はタブになり、それ以外の
// バイトの前のバックスラッシュは両方のバイトを残す。残したバックスラッシュと引用符はリテラルも
// 終えるため、格納された "\"" は部分を終え、その部分は自身の閉じる引用符を持たず、文字列の残りは
// 継続で続く。
internal static class Ax1Strings
{
    // The longest literal a continuation line holds: ASC reads 255 bytes of a line, and the
    // line adds two tabs and the "+".
    //
    // 継続の行が持てる最長のリテラル。ASC は行の 255 バイトを読み、行は 2 つのタブと "+" を足す。
    internal const int MaxLiteral = Ax1Layout.MaxLine - 3;

    private const byte Backslash = (byte)'\\';
    private const byte Quote = (byte)'"';
    private const byte Cr = 0x0d;
    private const byte Lf = 0x0a;
    private const byte Tab = 0x09;

    internal static Ax1Literal Spell(ReadOnlySpan<byte> stored)
    {
        ImmutableArray<string>.Builder pieces = ImmutableArray.CreateBuilder<string>();
        bool exact = true;
        foreach (Range range in Pieces(stored))
        {
            (string text, bool pieceExact) = Literal(stored[range]);
            pieces.Add(text);
            exact &= pieceExact;
        }

        return new Ax1Literal(pieces.ToImmutable(), exact);
    }

    // Cuts the string after each kept "\"" and wherever a piece would outgrow a continuation
    // line, at a point that splits neither an escape nor a Shift_JIS character.
    //
    // 残した "\"" の後ごと、および部分が継続の行を超える所で文字列を切る。切る位置はエスケープも
    // Shift_JIS の文字も分けない。
    private static List<Range> Pieces(ReadOnlySpan<byte> stored)
    {
        bool[] trail = TrailBytes(stored);
        var pieces = new List<Range>();
        int start = 0;
        int length = 2;
        int cut = 0;
        int i = 0;
        while (i < stored.Length)
        {
            (int size, int spelled) = Unit(stored, i);
            if (length + spelled > MaxLiteral && cut > start)
            {
                pieces.Add(start..cut);
                (start, length, i) = (cut, 2, cut);
                continue;
            }

            length += spelled;
            i += size;
            cut = trail[i] ? cut : i;
            if (
                size == 2
                && stored[i - 2] == Backslash
                && stored[i - 1] == Quote
                && i < stored.Length
            )
            {
                pieces.Add(start..i);
                (start, length, cut) = (i, 2, i);
            }
        }

        pieces.Add(start..stored.Length);
        return pieces;
    }

    // The bytes one source unit stores, and the bytes its spelling takes.
    //
    // 1 つのソースの単位が格納するバイト数と、その綴りが取るバイト数。
    private static (int Size, int Spelled) Unit(ReadOnlySpan<byte> stored, int i)
    {
        byte b = stored[i];
        bool pair = i + 1 < stored.Length && (b == Backslash || (b == Cr && stored[i + 1] == Lf));
        return pair ? (2, 2) : (1, b is Tab or Cr or Lf or Quote ? 2 : 1);
    }

    // Marks each byte that is the second byte of a Shift_JIS character; index Length is false.
    //
    // Shift_JIS の文字の 2 バイト目である各バイトに印を付ける。添字 Length は false。
    private static bool[] TrailBytes(ReadOnlySpan<byte> stored)
    {
        bool[] trail = new bool[stored.Length + 1];
        for (int i = 0; i + 1 < stored.Length; i++)
        {
            if (stored[i] is (>= 0x81 and <= 0x9f) or (>= 0xe0 and <= 0xfc))
            {
                trail[++i] = true;
            }
        }

        return trail;
    }

    // The literal of one piece, and false when no source literal yields exactly these bytes.
    //
    // 1 つの部分のリテラル。どのソースのリテラルもちょうどこのバイト列を作らない時は false も返す。
    private static (string Text, bool Exact) Literal(ReadOnlySpan<byte> stored)
    {
        var output = new List<byte> { Quote };
        bool exact = true;
        bool closed = false;
        for (int i = 0; i < stored.Length; i++)
        {
            byte b = stored[i];
            closed = false;
            if (b == Backslash)
            {
                bool paired = i + 1 < stored.Length;
                exact &= paired && !IsInterpreted(stored[i + 1]);
                output.Add(Backslash);
                if (paired)
                {
                    output.Add(stored[++i]);
                    closed = stored[i] == Quote;
                }
            }
            else if (b == Cr && i + 1 < stored.Length && stored[i + 1] == Lf)
            {
                output.AddRange([Backslash, (byte)'n']);
                i++;
            }
            else
            {
                exact &= b is not (Cr or Lf or Quote);
                output.AddRange(
                    b switch
                    {
                        Tab => [Backslash, (byte)'t'],
                        Cr or Lf => [Backslash, (byte)'n'],
                        Quote => [Backslash, Quote],
                        _ => [b],
                    }
                );
            }
        }

        if (!closed || !exact)
        {
            output.Add(Quote);
        }

        byte[] bytes = [.. output];
        string text = ShiftJisHelper.Encoding.GetString(bytes);
        return (
            text,
            exact && ShiftJisHelper.Encoding.GetBytes(text).AsSpan().SequenceEqual(bytes)
        );
    }

    // A byte ASC would read as part of an escape or a line break after a backslash.
    //
    // バックスラッシュの後で ASC がエスケープや改行の一部として読むバイト。
    private static bool IsInterpreted(byte b) =>
        b is (byte)'n' or (byte)'N' or (byte)'t' or (byte)'T' or Cr or Lf;
}
