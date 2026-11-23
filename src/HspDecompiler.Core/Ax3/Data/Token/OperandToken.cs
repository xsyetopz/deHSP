namespace HspDecompiler.Core.Ax3.Data.Token;

internal abstract class OperandToken : ExpressionTermToken
{
    internal override bool IsOperand => true;

    internal override bool IsOperator => false;
}
