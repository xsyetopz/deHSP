using System;
using System.Collections.Generic;
using System.Linq;
using HspDecompiler.Core.Ax2.Data;

namespace HspDecompiler.Core.Ax2.Writing;

// Folds each "{ }" block the emitter wrote onto its if or else line, joined with " : ", for a
// release whose if ends at the end of its line. "if c {", "x", "}", "else {", "y", "}" becomes
// "if c : x : else : y", and a nested block folds first.
//
// if が行末で終わるリリースのために、エミッターが書いた "{ }" の各ブロックを " : " でつないで
// if または else の行に畳む。"if c {"、"x"、"}"、"else {"、"y"、"}" は "if c : x : else : y"
// になり、入れ子のブロックが先に畳まれる。
internal static class LineIfs
{
    private const string Open = " {";
    private const string Close = "}";
    private const string Else = "else";

    private sealed class Block(string indent, List<SourceLine> parts)
    {
        internal string Indent { get; } = indent;

        internal List<SourceLine> Parts { get; } = parts;

        internal SourceLine Fold() =>
            new(
                Indent + string.Join(" : ", Parts.Select(p => p.Text)),
                Parts.Select(p => p.Anchor).FirstOrDefault(a => a >= 0, -1)
            );
    }

    internal static List<SourceLine> Fold(List<SourceLine> lines)
    {
        var output = new List<SourceLine>(lines.Count);
        var open = new Stack<Block>();
        foreach (SourceLine line in lines)
        {
            string text = line.Text.TrimStart('\t');
            string indent = line.Text[..(line.Text.Length - text.Length)];
            List<SourceLine> target = open.Count > 0 ? open.Peek().Parts : output;
            if (text == Close && open.Count > 0)
            {
                SourceLine folded = open.Pop().Fold();
                if (open.Count > 0)
                {
                    open.Peek().Parts.Add(Strip(folded));
                }
                else
                {
                    output.Add(folded);
                }
            }
            else if (text == Else + Open)
            {
                SourceLine ifLine = target[^1];
                target.RemoveAt(target.Count - 1);
                open.Push(new Block(indent, [Strip(ifLine), new SourceLine(Else, -1)]));
            }
            else if (text.EndsWith(Open, StringComparison.Ordinal))
            {
                open.Push(new Block(indent, [new SourceLine(text[..^Open.Length], line.Anchor)]));
            }
            else
            {
                target.Add(open.Count > 0 ? new SourceLine(text, line.Anchor) : line);
            }
        }

        return output;
    }

    private static SourceLine Strip(SourceLine line) =>
        line with
        {
            Text = line.Text.TrimStart('\t'),
        };
}
