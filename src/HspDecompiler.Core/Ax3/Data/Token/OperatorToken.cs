using HspDecompiler.Core.Ax3.Data.Primitive;
using HspDecompiler.Core.Pipeline;

namespace HspDecompiler.Core.Ax3.Data.Token;

internal sealed class OperatorToken : ExpressionTermToken
{
    internal OperatorToken(OperatorPrimitive source)
    {
        _primitive = source;
    }

    private readonly OperatorPrimitive? _primitive;
    internal override int TokenOffset => _primitive!.TokenOffset;

    internal SourceStyle Style => _primitive!.Style;

    public override string ToString() => _primitive!.ToString();

    internal CalcCode Code => _primitive!.Code;

    // An assignment spells its operator with '=' after it ("+=") except for '=' itself and the
    // two comparisons hspcmp also reads there; a bare + or - is an increment or decrement. In an
    // expression '=' and '!' compare, written "==" and "!=".
    //
    // 代入では演算子の後に '=' を付ける("+=")。'=' 自体と、hspcmp がそこでも読む 2 つの比較は
    // 除く。式の無い + と - はインクリメントとデクリメント。式の中の '=' と '!' は比較で、
    // "==" と "!=" と書く。
    internal string ToString(bool isAssignment, bool hasExpression)
    {
        string text = _primitive!.ToString();
        return _primitive.CodeType != HspCodeType.Operator ? text
            : isAssignment
                ? (Code, hasExpression) switch
                {
                    (CalcCode.Add, false) => "++",
                    (CalcCode.Subtract, false) => "--",
                    (CalcCode.Equal or CalcCode.Greater or CalcCode.Less, _) => text,
                    _ => text + "=",
                }
            : Code is CalcCode.Equal or CalcCode.NotEqual ? text + "="
            : text;
    }

    internal override bool IsOperand => false;

    internal override bool IsOperator => true;

    internal override int Priority => _primitive!.OperatorPriority;
}
