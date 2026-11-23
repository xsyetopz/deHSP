using HspDecompiler.Core.Ax3.Data.Primitive;
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
    private string RenderInfix(RenderContext context)
    {
        SourceStyle style = context.Style;
        int priority = _op!.Priority(context);
        bool logical = style.LogicalOperators != LogicalOperators.Symbols && IsLogical;
        string left = Operand(
            context,
            _p1!,
            _p1!.Priority(context) < priority || (logical && _p1.IsComparison)
        );
        string right = Operand(
            context,
            _p2!,
            _p2!.Priority(context) <= priority || (logical && _p2.IsComparison)
        );
        string op = logical
            ? LogicalText(style.LogicalOperators)
            : _op.Render(context, false, true);
        bool compact =
            style.CompactOperators
            && !(logical && style.LogicalOperators == LogicalOperators.Words);
        return !compact ? left + " " + op + " " + right
            : right.StartsWith('-') ? left + op + " " + right
            : left + op + right;
    }

    private static string Operand(RenderContext context, OperandToken operand, bool parenthesize) =>
        parenthesize ? "(" + operand.Render(context) + ")" : operand.Render(context);

    private string LogicalText(LogicalOperators logical) =>
        (_op!.Code, logical) switch
        {
            (CalcCode.And, LogicalOperators.Words) => "and",
            (_, LogicalOperators.Words) => "or",
            (CalcCode.And, _) => "&&",
            _ => "||",
        };

    // & and | between two conditions read as logical operators; between other values they stay
    // bitwise.
    //
    // 二つの条件の間の & と | は論理演算子として読む。それ以外の値の間ではビット演算のまま。
    private bool IsLogical =>
        _op!.Code is CalcCode.And or CalcCode.Or && IsCondition(_p1!) && IsCondition(_p2!);

    private static bool IsCondition(OperandToken operand) =>
        operand.IsComparison || operand is SubExpressionToken { IsLogical: true };

    // var * -1 reads back as -var, which hspcmp compiles to the same multiplication.
    //
    // var * -1 は -var と書く。hspcmp は同じ乗算にコンパイルする。
    internal override string Render(RenderContext context) =>
        _op!.Code == CalcCode.Multiply
        && (
            (_p1 is LiteralToken { IsMinusOne: true } && _p2 is VariableToken right) ? right
            : (_p2 is LiteralToken { IsMinusOne: true } && _p1 is VariableToken left) ? left
            : null
        )
            is { } negated
            ? "-" + negated.Render(context)
            : RenderInfix(context);

    internal override int Priority(RenderContext context) => _op!.Priority(context);

    internal override bool IsComparison => _op!.Code.IsComparison();

    internal override void CheckLabel()
    {
        _p1?.CheckLabel();

        _p2?.CheckLabel();

        _op?.CheckLabel();
    }
}
