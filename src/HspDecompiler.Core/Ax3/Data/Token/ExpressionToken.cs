using System.Collections.Generic;
using System.Linq;

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

    // The literal the expression consists of, or null when it holds more than one term.
    //
    // 式を成すリテラル。項が二つ以上なら null。
    internal LiteralToken? Literal => _tokens.Count == 1 ? _tokens[0] as LiteralToken : null;

    internal override int TokenOffset => _tokens.Count == 0 ? -1 : _tokens[0].TokenOffset;

    // An expression the RPN fold could not take apart is written term by term in stored order.
    //
    // RPN の畳み込みで分解できなかった式は、格納順に項を並べて書く。
    internal override string Render(RenderContext context) =>
        _convertedToken != null
            ? _convertedToken.Render(context)
            : string.Join(' ', _tokens.Select(token => token.Render(context)));

    internal override void CheckLabel()
    {
        foreach (CodeToken token in _tokens)
        {
            token.CheckLabel();
        }
    }

    internal override bool CheckRpn() => CanRpnConvert;
}
