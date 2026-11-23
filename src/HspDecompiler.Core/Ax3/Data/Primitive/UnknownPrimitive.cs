namespace HspDecompiler.Core.Ax3.Data.Primitive;

internal sealed class UnknownPrimitive : PrimitiveToken
{
    internal UnknownPrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet) { }

    internal override string Render(RenderContext context) => DefaultName;
}
