using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HspDecompiler.Core.Ax1.Data;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax1.Writing;

// Writes one statement's tokens back as source. ASC writes an operator character c as the byte
// c + 0x70, and the commands 0xb0 to 0xb7 except local switch it to label mode, where a new name
// becomes a label and it drops operators and commas; so a label argument is written with "*"
// and arguments there are joined with ",".
//
// 1 つの文のトークンをソースに書き戻す。ASC は演算子の文字 c をバイト c + 0x70 として書き、
// local を除く 0xb0〜0xb7 の命令はラベルのモードに切り替える。そこでは新しい名前はラベルになり、
// 演算子とコンマは捨てられる。そのためラベルの引数は "*" 付きで書き、引数は "," でつなぐ。
internal sealed class Ax1StatementWriter(Ax1Context context)
{
    private const int Local = 0xb3;
    private const int Nop = 0xed;
    private const int Sprdraw = 0xbf;
    private const int Backslash = 0xcc;

    private static readonly FrozenDictionary<int, char> s_operators =
        "!#%&()*+,-./<=>?@[\\]^`{|}~".ToFrozenDictionary(c => c + 0x70, c => c);

    private readonly StringBuilder _text = new();
    private List<string> _prefix = [];
    private bool _labelMode;
    private bool _inLocal;
    private bool _lastWord;
    private bool _createdLabel;
    private int _labelArgs;

    // The last string the last statement wrote, where its inline literal starts in the
    // statement's source; null when it wrote none.
    //
    // 最後の文が最後に書いた文字列と、文のソースでのその行内のリテラルの開始位置。書かなければ
    // null。
    internal Ax1LineString? LastString { get; private set; }

    // The statement's source, with what has to precede it on the line, such as an alias's
    // define, added to prefix.
    //
    // 文のソース。行でその前に置くべきもの(別名の define など)は prefix に加える。
    internal string Write(Ax1Statement statement, List<string> prefix)
    {
        _text.Clear();
        _prefix = prefix;
        _labelMode = _inLocal = _lastWord = _createdLabel = false;
        LastString = null;
        if (statement.IsElse)
        {
            return context.Keyword(0, 0x80);
        }

        if (Ax1Context.IsLoop(statement))
        {
            if (!context.RepeatEnds.Contains(statement.Tokens[2].Value))
            {
                context.Differs(statement.Offset, Strings.Ax1JumpTarget);
            }

            return context.Keyword(0, Ax1Context.Loop);
        }

        int i = WriteHead(statement);
        while (i < statement.Tokens.Length)
        {
            i = WriteToken(statement, i);
        }

        return _text.ToString();
    }

    // A byte at the start of a statement is a command when the release names it, so 0xac is
    // the map command mspleft in 1.2f and the operator "<" in 1.1; "\" there starts label mode.
    //
    // 文の先頭のバイトはリリースが名前を付けていれば命令である。そのため 0xac は 1.2f では
    // マップの命令 mspleft、1.1 では演算子 "<" になる。そこでの "\" はラベルのモードを始める。
    private int WriteHead(Ax1Statement statement)
    {
        if (statement.Tokens.IsEmpty || statement.Tokens[0].Kind != Ax1TokenKind.Byte)
        {
            return 0;
        }

        int b = statement.Tokens[0].Value;
        if (
            context.IsBreak(statement, 0)
            || (b == Nop && statement.Tokens.Length > 1 && statement.Tokens[1].IsByte(Sprdraw))
        )
        {
            return 0;
        }

        if (context.Dictionary.ExactNameOf(0, b, context.Release) is { } name)
        {
            Command(b, name);
            return 1;
        }

        if (b == Backslash)
        {
            Operator('\\');
            _labelMode = true;
            _labelArgs = 0;
            return 1;
        }

        return 0;
    }

    private int WriteToken(Ax1Statement statement, int i)
    {
        Ax1Token token = statement.Tokens[i];
        switch (token.Kind)
        {
            case Ax1TokenKind.Byte:
                return WriteByte(statement, i);
            case Ax1TokenKind.Number:
                Word(token.Value.ToString(CultureInfo.InvariantCulture));
                break;
            case Ax1TokenKind.WideNumber:
                Differ(token.Value < 0x8000, token, Strings.Ax1WideNumber);
                Word(token.Value.ToString(CultureInfo.InvariantCulture));
                break;
            case Ax1TokenKind.Variable:
                Word(context.Symbols.Global(token.Value, !_labelMode && !_inLocal, _prefix));
                break;
            case Ax1TokenKind.Local:
                (string local, bool inOrder) = context.Symbols.Local(
                    token.Value,
                    _inLocal && !_labelMode
                );
                Differ(!inOrder, token, Strings.Ax1VariableOrder);
                Word(local);
                break;
            case Ax1TokenKind.String:
                WriteString(token);
                break;
            case Ax1TokenKind.LabelOffset:
                (string label, bool isNew) = context.Symbols.At(token.Value);
                Differ(isNew && !_labelMode, token, Strings.Ax1LabelBeforeUse);
                Label(label);
                break;
            case Ax1TokenKind.LabelIndex:
                (string indexed, bool ordered) = context.Symbols.Indexed(
                    token.Value,
                    !context.Program.Unresolved,
                    _prefix,
                    ref _createdLabel
                );
                Differ(!ordered, token, Strings.Ax1LabelIndexOrder);
                Label(indexed);
                break;
            case Ax1TokenKind.If:
                Word(context.Keyword(0, 0xfc));
                break;
            case Ax1TokenKind.Line:
            case Ax1TokenKind.Unknown:
            default:
                context.Differs(token.Offset, Strings.Ax1UnknownToken);
                break;
        }

        return i + 1;
    }

    private int WriteByte(Ax1Statement statement, int i)
    {
        int b = statement.Tokens[i].Value;
        if (context.IsBreak(statement, i))
        {
            Word(context.Keyword(0, 0xb9));
            return i + 2;
        }

        if (b == Nop && i + 1 < statement.Tokens.Length && statement.Tokens[i + 1].IsByte(Sprdraw))
        {
            Command(Sprdraw, context.Keyword(0, Sprdraw));
            return i + 2;
        }

        if (s_operators.TryGetValue(b, out char op))
        {
            Operator(op);
        }
        else if (context.Dictionary.NameOf(0, b, context.Release) is { } name)
        {
            Command(b, name);
        }
        else
        {
            context.Differs(statement.Tokens[i].Offset, Strings.Ax1UnknownToken);
        }

        return i + 1;
    }

    private void WriteString(Ax1Token token)
    {
        int end = Array.IndexOf(context.Program.Strings, (byte)0, token.Value);
        if (token.Value >= context.Program.Strings.Length || end < 0)
        {
            context.Differs(token.Offset, Strings.Ax1StringBytes);
            Word("\"\"");
            return;
        }

        Ax1Literal literal = Ax1Strings.Spell(
            context.Program.Strings.AsSpan(token.Value, end - token.Value)
        );
        Differ(!literal.Exact, token, Strings.Ax1StringBytes);
        Abandon(LastString);
        Word(literal.Inline);
        LastString = new Ax1LineString(literal, _text.Length - literal.Inline.Length, token.Offset);
    }

    // A string with continuations that a later string follows loses them, since a continuation
    // appends to the string ASC stored last.
    //
    // 後に別の文字列が続く、継続を持つ文字列は継続を失う。継続は ASC が最後に格納した文字列に
    // 足されるため。
    internal void Abandon(Ax1LineString? last)
    {
        if (last is { Literal.Continuations.IsEmpty: false })
        {
            context.Differs(last.Offset, Strings.Ax1StringBytes);
        }
    }

    private void Command(int b, string name)
    {
        Word(name);
        if (b is >= 0xb0 and < 0xb8 and not Local)
        {
            _labelMode = true;
            _labelArgs = 0;
        }

        _inLocal |= b == Local;
    }

    private void Label(string name) => Word(_labelMode ? "*" + name : name);

    private void Word(string text)
    {
        if (_lastWord)
        {
            _text.Append(_labelMode && _labelArgs > 0 ? ',' : ' ');
        }

        _labelArgs += _labelMode ? 1 : 0;
        _text.Append(text);
        _lastWord = true;
    }

    private void Operator(char op)
    {
        _text.Append(op);
        _lastWord = false;
    }

    private void Differ(bool differs, Ax1Token token, string reason)
    {
        if (differs)
        {
            context.Differs(token.Offset, reason);
        }
    }
}
