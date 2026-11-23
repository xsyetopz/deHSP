namespace HspDecompiler.Core.Ax3.Data.Primitive;

internal sealed class UnknownPrimitive : PrimitiveToken
{
    internal UnknownPrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet) { }

    public override string ToString() => DefaultName;
}
