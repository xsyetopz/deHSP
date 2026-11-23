using System.Text;

namespace HspDecompiler.Core.Ax3.Data.Token;

internal sealed class SubExpressionToken : OperandToken
{
    internal SubExpressionToken(
        OperandToken leftToken,
        OperandToken rightToken,
        OperatorToken opToken
    )
    {
        _p1 = leftToken;
        _p2 = rightToken;
        _op = opToken;
    }

    private readonly OperandToken? _p1;
    private readonly OperandToken? _p2;
    private readonly OperatorToken? _op;

    internal override int TokenOffset => _p1!.TokenOffset;

    private string ToStringForceDefault()
    {
        var builder = new StringBuilder();
        if (_p1!.Priority < _op!.Priority)
        {
            builder.Append('(');
            builder.Append(_p1);
            builder.Append(')');
        }
        else
        {
            builder.Append(_p1);
        }
        builder.Append(' ');
        builder.Append(_op.ToString(false, true));
        builder.Append(' ');
        if (_p2!.Priority <= _op.Priority)
        {
            builder.Append('(');
            builder.Append(_p2);
            builder.Append(')');
        }
        else
        {
            builder.Append(_p2);
        }
        return builder.ToString();
    }

    internal string ToString(bool forceDefault)
    {
        if (forceDefault)
        {
            return ToStringForceDefault();
        }

        var lit = _p1 as LiteralToken;
        var var = _p2 as VariableToken;
        if ((lit == null) || (var == null))
        {
            lit = _p2 as LiteralToken;
            var = _p1 as VariableToken;
        }
        if ((lit == null) || (var == null))
        {
            return ToStringForceDefault();
        }

        if (!lit.IsMinusOne)
        {
            return ToStringForceDefault();
        }

        if (_op!.ToString() != "*")
        {
            return ToStringForceDefault();
        }

        var builder = new StringBuilder();
        builder.Append('-');
        builder.Append(var);
        return builder.ToString();
    }

    public override string ToString() => ToString(false);

    internal override int Priority => _op!.Priority;

    internal override void CheckLabel()
    {
        _p1?.CheckLabel();

        _p2?.CheckLabel();

        _op?.CheckLabel();
    }
}
