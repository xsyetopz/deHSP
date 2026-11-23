using System.Text;
using HspDecompiler.Core.Pipeline;

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

    // A negative right operand keeps a space before it in the compact style, since "a--1" reads
    // as a decrement to a person even though hspcmp reads it as "a - -1".
    //
    // 詰めた書き方でも負の右オペランドの前には空白を残す。hspcmp は "a--1" を "a - -1" と
    // 読むが、人にはデクリメントに見えるためである。
    private string ToStringForceDefault()
    {
        SourceStyle style = _op!.Style;
        bool logical = style.LogicalOperators != LogicalOperators.Symbols && IsLogical;
        string left = Operand(_p1!, _p1!.Priority < _op.Priority || (logical && _p1.IsComparison));
        string right = Operand(
            _p2!,
            _p2!.Priority <= _op.Priority || (logical && _p2.IsComparison)
        );
        string op = logical ? LogicalText(style.LogicalOperators) : _op.ToString(false, true);
        bool compact =
            style.CompactOperators
            && !(logical && style.LogicalOperators == LogicalOperators.Words);
        return !compact ? left + " " + op + " " + right
            : right.StartsWith('-') ? left + op + " " + right
            : left + op + right;
    }

    private static string Operand(OperandToken operand, bool parenthesize) =>
        parenthesize ? "(" + operand + ")" : operand.ToString();

    private string LogicalText(LogicalOperators logical) =>
        (_op!.ToString(), logical) switch
        {
            ("&", LogicalOperators.Words) => "and",
            (_, LogicalOperators.Words) => "or",
            ("&", _) => "&&",
            _ => "||",
        };

    // & and | between two conditions read as logical operators; between other values they stay
    // bitwise.
    //
    // 二つの条件の間の & と | は論理演算子として読む。それ以外の値の間ではビット演算のまま。
    private bool IsLogical =>
        _op!.ToString() is "&" or "|" && IsCondition(_p1!) && IsCondition(_p2!);

    private static bool IsCondition(OperandToken operand) =>
        operand.IsComparison || operand is SubExpressionToken { IsLogical: true };

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

    internal override bool IsComparison => _op!.Priority == ComparisonPriority;

    private const int ComparisonPriority = 1;

    internal override void CheckLabel()
    {
        _p1?.CheckLabel();

        _p2?.CheckLabel();

        _op?.CheckLabel();
    }
}
