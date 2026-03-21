namespace HspDecompiler.Core.Ax3.Data.Token;

internal abstract class ExpressionTermToken : CodeToken
{
    internal abstract bool IsOperand { get; }
    internal abstract bool IsOperator { get; }
    internal virtual bool IsLabel => false;
    internal abstract int Priority { get; }
}
