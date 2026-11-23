using System.Collections.Generic;
using System.Text;

namespace HspDecompiler.Core.Ax3.Data.Layout;

// Writes string literals back in the {"..."} form a debug build shows they had. hspcmp turns
// each line break inside {"..."} into CR LF, the same bytes as the "\n" escape, and records the
// line where the string ended (codegen.cpp PickLongStringCG). So a string that ended k lines
// after it started gets k of its "\n" escapes back as real line breaks; which k makes no
// difference to the bytes. The strings take the recorded ends in source order.
//
// デバッグビルドが示す {"..."} 形式で文字列リテラルを書き戻す。hspcmp は {"..."} の中の
// 改行を "\n" エスケープと同じ CR LF にし、文字列が終わった行を記録する(codegen.cpp の
// PickLongStringCG)。そのため開始から k 行後に終わった文字列は、"\n" エスケープのうち k 個
// を本当の改行に戻す。どの k 個でもバイト列は変わらない。文字列はソース順に記録された終わりを
// 受け取る。
internal static class LongStrings
{
    private const int EscapeLength = 2;

    // Line breaks each recorded {"..."} string held, in source order.
    //
    // 記録された各 {"..."} 文字列が持っていた改行の数をソース順に並べる。
    internal static Queue<int> Breaks(DebugLine record)
    {
        var breaks = new Queue<int>();
        int previous = record.Line;
        foreach (int end in record.StringEnds ?? [])
        {
            breaks.Enqueue(end - previous);
            previous = end;
        }

        return breaks;
    }

    // Rewrites the literals of one statement, taking breaks from the queue while they fit.
    //
    // 1 つの文のリテラルを書き換え、収まる限りキューから改行の数を取る。
    internal static string Render(string text, Queue<int> breaks)
    {
        if (breaks.Count == 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (c is not ('"' or '\''))
            {
                builder.Append(c);
                i++;
                continue;
            }

            int end = SkipQuoted(text, i);
            string literal = text[i..end];
            bool takes = c == '"' && breaks.Count > 0 && CountEscapes(literal) >= breaks.Peek();
            builder.Append(takes ? Long(literal, breaks.Dequeue()) : literal);
            i = end;
        }

        return builder.ToString();
    }

    // The index just past the literal that starts at the given quote.
    //
    // 指定した引用符で始まるリテラルの直後の位置。
    private static int SkipQuoted(string text, int start)
    {
        char quote = text[start];
        int i = start + 1;
        while (i < text.Length && text[i] != quote)
        {
            i += text[i] == '\\' ? 2 : 1;
        }

        return i < text.Length ? i + 1 : text.Length;
    }

    private static int CountEscapes(string literal)
    {
        int count = 0;
        for (int i = 1; i < literal.Length - 1; i++)
        {
            if (literal[i] == '\\')
            {
                count += literal[i + 1] == 'n' ? 1 : 0;
                i++;
            }
        }

        return count;
    }

    private static string Long(string literal, int breaks)
    {
        var builder = new StringBuilder("{\"", literal.Length + 2);
        int left = breaks;
        for (int i = 1; i < literal.Length - 1; i++)
        {
            if (literal[i] != '\\')
            {
                builder.Append(literal[i]);
                continue;
            }

            bool lineBreak = left > 0 && literal[i + 1] == 'n';
            builder.Append(lineBreak ? "\n" : literal.Substring(i, EscapeLength));
            left -= lineBreak ? 1 : 0;
            i++;
        }

        return builder.Append("\"}").ToString();
    }
}
