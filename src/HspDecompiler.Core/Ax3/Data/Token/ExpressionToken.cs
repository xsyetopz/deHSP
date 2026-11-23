using System.Collections.Generic;
using System.Text;

namespace HspDecompiler.Core.Ax3.Data.Token;

internal sealed class ExpressionToken : CodeToken
{
    internal ExpressionToken(List<ExpressionTermToken> elements)
    {
        _tokens = elements;
    }

    private readonly List<ExpressionTermToken> _tokens;
    private ExpressionTermToken? _convertedToken;
    private bool _tryConvert;
    internal bool CanRpnConvert => _convertedToken != null || !_tryConvert && RpnConvert();

    internal bool RpnConvert()
    {
        if (_convertedToken != null)
        {
            return true;
        }

        if (_tokens.Count == 0)
        {
            return false;
        }

        _tryConvert = true;
        if (_tokens.Count == 1)
        {
            _convertedToken = _tokens[0];
            return true;
        }
        _convertedToken = Fold(_tokens);
        return _convertedToken != null;
    }

    // Folds the RPN term list into one tree, or returns null when an operator lacks two operands
    // or terms remain.
    //
    // RPN の項の並びを一つの木にまとめる。演算子に二つのオペランドが無い場合や項が余る場合は
    // null を返す。
    private static ExpressionTermToken? Fold(List<ExpressionTermToken> tokens)
    {
        var stack = new Stack<ExpressionTermToken>();
        foreach (ExpressionTermToken token in tokens)
        {
            if (!token.IsOperator)
            {
                stack.Push(token);
                continue;
            }

            if (
                (token is not OperatorToken op)
                || !stack.TryPop(out ExpressionTermToken? rightTerm)
                || !stack.TryPop(out ExpressionTermToken? leftTerm)
                || (rightTerm is not OperandToken right)
                || (leftTerm is not OperandToken left)
            )
            {
                return null;
            }

            stack.Push(new SubExpressionToken(left, right, op));
        }

        return stack.Count == 1 ? stack.Pop() : null;
    }

    internal override int TokenOffset => _tokens.Count == 0 ? -1 : _tokens[0].TokenOffset;

    internal string ToString(bool getRpnConverted)
    {
        if (getRpnConverted && (_convertedToken != null))
        {
            return _convertedToken.ToString();
        }

        var builder = new StringBuilder();
        int i = 0;
        foreach (ExpressionTermToken token in _tokens)
        {
            if (i != 0)
            {
                builder.Append(' ');
            }

            builder.Append(token);
            i++;
        }
        return builder.ToString();
    }

    public override string ToString() => ToString(true);

    internal override void CheckLabel()
    {
        foreach (CodeToken token in _tokens)
        {
            token.CheckLabel();
        }
    }

    internal override bool CheckRpn() => CanRpnConvert;
}
