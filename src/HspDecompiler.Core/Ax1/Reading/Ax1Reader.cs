using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using HspDecompiler.Core.Ax1.Data;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax1.Reading;

// Reads an "Aspo" image into statements and line markers, following the token sizes ASC's own
// label pass walks by.
//
// "Aspo" イメージを文と行の印に読む。ASC 自身のラベルの処理が辿るトークンの大きさに従う。
internal static class Ax1Reader
{
    private const int HeaderSize = 0x20;
    private const int StatementEnd = 0x80;
    private const int ProgramEnd = 0x8f;

    internal static Ax1Program Read(byte[] bytes)
    {
        if (bytes.Length < HeaderSize)
        {
            throw new HspDecoderException(Strings.NotAxImage);
        }

        int codeSize = Word(bytes, 6);
        if (HeaderSize + codeSize > bytes.Length || codeSize == 0)
        {
            throw new HspDecoderException(Strings.NotAxImage);
        }

        ReadOnlySpan<byte> code = bytes.AsSpan(HeaderSize, codeSize);
        List<Ax1Token> tokens = Tokenize(code);
        (ImmutableArray<Ax1Statement> statements, ImmutableArray<Ax1Token> lines) = Split(tokens);
        int firstEnd = tokens.Find(t => t.IsByte(ProgramEnd)).Offset;
        return new Ax1Program(
            Word(bytes, 8),
            Word(bytes, 10),
            statements,
            lines,
            codeSize - 1,
            bytes[(HeaderSize + codeSize)..]
        )
        {
            FirstEnd = firstEnd,
            Unresolved = tokens.Exists(t =>
                t.Kind == Ax1TokenKind.LabelIndex && t.Offset < firstEnd
            ),
        };
    }

    private static int Word(byte[] bytes, int at) => bytes[at] | (bytes[at + 1] << 8);

    private static List<Ax1Token> Tokenize(ReadOnlySpan<byte> code)
    {
        var tokens = new List<Ax1Token>();
        int at = 0;
        while (at < code.Length)
        {
            Ax1Token token = Next(code, at);
            if (token.End > code.Length)
            {
                throw new HspDecoderException(Strings.NotAxImage);
            }

            tokens.Add(token);
            at = token.End;
        }

        return tokens;
    }

    private static Ax1Token Next(ReadOnlySpan<byte> code, int at)
    {
        byte first = code[at];
        if (first < 0x80)
        {
            return new(
                Ax1TokenKind.Number,
                at,
                2,
                at + 1 < code.Length ? (first << 8) | code[at + 1] : 0
            );
        }

        if (first < 0xf0)
        {
            return new(Ax1TokenKind.Byte, at, 1, first);
        }

        if (first == 0xf1)
        {
            return new(Ax1TokenKind.Unknown, at, 2, first);
        }

        int value = at + 2 < code.Length ? code[at + 1] | (code[at + 2] << 8) : 0;
        Ax1TokenKind kind = first switch
        {
            0xff => Ax1TokenKind.WideNumber,
            0xf3 => Ax1TokenKind.Variable,
            0xf6 => Ax1TokenKind.Local,
            0xfd => Ax1TokenKind.String,
            0xf5 => Ax1TokenKind.LabelOffset,
            0xf4 => Ax1TokenKind.LabelIndex,
            0xfc => Ax1TokenKind.If,
            0xf7 => Ax1TokenKind.Line,
            _ => Ax1TokenKind.Unknown,
        };
        return new(kind, at, 3, kind == Ax1TokenKind.Unknown ? first : value);
    }

    // Splits the tokens at each 0x80. A line marker stands between statements, and the last
    // token, the lone 0x8f that ends the program, belongs to none.
    //
    // 各 0x80 でトークンを分ける。行の印は文と文の間に立ち、最後のトークン(プログラムを終える
    // 単独の 0x8f)はどの文にも属さない。
    private static (ImmutableArray<Ax1Statement>, ImmutableArray<Ax1Token>) Split(
        List<Ax1Token> tokens
    )
    {
        ImmutableArray<Ax1Statement>.Builder statements =
            ImmutableArray.CreateBuilder<Ax1Statement>();
        ImmutableArray<Ax1Token>.Builder lines = ImmutableArray.CreateBuilder<Ax1Token>();
        int last = tokens.Count - 1;
        int i = 0;
        while (i < last)
        {
            if (tokens[i].Kind == Ax1TokenKind.Line)
            {
                lines.Add(tokens[i++]);
                continue;
            }

            int start = tokens[i].Offset;
            bool isElse = tokens[i].IsByte(StatementEnd);
            i += isElse ? 1 : 0;
            ImmutableArray<Ax1Token>.Builder body = ImmutableArray.CreateBuilder<Ax1Token>();
            while (
                i < last && !tokens[i].IsByte(StatementEnd) && tokens[i].Kind != Ax1TokenKind.Line
            )
            {
                body.Add(tokens[i++]);
            }

            int end =
                i < last && tokens[i].IsByte(StatementEnd) ? tokens[i++].End : tokens[i].Offset;
            statements.Add(new Ax1Statement(start, end, body.ToImmutable(), isElse));
        }

        return (statements.ToImmutable(), lines.ToImmutable());
    }
}
