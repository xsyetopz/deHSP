namespace HspDecompiler.Core.Ax3.Data.Primitive;

internal sealed class OperatorPrimitive : PrimitiveToken
{
    internal OperatorPrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet) { }

    internal CalcCode Code => (CalcCode)Value;
}
