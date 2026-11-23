namespace HspDecompiler.Core.Ax3.Data.Primitive;

internal class HspFunctionPrimitive : FunctionPrimitive
{
    protected HspFunctionPrimitive() { }

    internal HspFunctionPrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet) { }
}

internal sealed class OnFunctionPrimitive : HspFunctionPrimitive
{
    internal OnFunctionPrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet) { }
}

internal sealed class OnEventFunctionPrimitive : HspFunctionPrimitive
{
    internal OnEventFunctionPrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet) { }
}

internal sealed class McallFunctionPrimitive : HspFunctionPrimitive
{
    internal McallFunctionPrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet) { }
}
