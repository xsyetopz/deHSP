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

    internal string ToString(bool isAssignment, bool hasExpression)
    {
        string ret = _primitive!.ToString();
        if (_primitive.CodeType != HspCodeType.Operator)
        {
            return _primitive.ToString();
        }

        if (isAssignment)
        {
            if ((!hasExpression) && (ret == "+"))
            {
                return "++";
            }
            else if ((!hasExpression) && (ret == "-"))
            {
                return "--";
            }

            return ret switch
            {
                "=" or ">" or "<" => ret,
                _ => ret + "=",
            };
        }
        else if (ret is "=" or "!")
        {
            return ret + "=";
        }
        return ret;
    }

    internal override bool IsOperand => false;

    internal override bool IsOperator => true;

    internal override int Priority => _primitive!.OperatorPriority;
}
