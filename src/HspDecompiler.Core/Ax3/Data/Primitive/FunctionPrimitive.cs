using HspDecompiler.Core.Ax3.Data.PP;

namespace HspDecompiler.Core.Ax3.Data.Primitive;

internal abstract class FunctionPrimitive : PrimitiveToken
{
    protected FunctionPrimitive() { }

    internal FunctionPrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet) { }
}

internal sealed class UserFunctionPrimitive : FunctionPrimitive
{
    internal UserFunctionPrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet)
    {
        _func = dataSet._parent!.GetFunction(Value);
        _func?.MarkUsedAt(TokenOffset);
    }

    private readonly Function? _func;

    internal override string Render(RenderContext context) =>
        _func?.FunctionName is { } name ? context.Function(name) : DefaultName;
}

internal sealed class DllFunctionPrimitive : FunctionPrimitive
{
    internal DllFunctionPrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet)
    {
        _func = dataSet._parent!.GetFunction(Value);
        _func?.MarkUsedAt(TokenOffset);
    }

    private readonly Function? _func;

    internal override string Render(RenderContext context) =>
        _func?.FunctionName is { } name ? context.Import(name) : DefaultName;
}

internal sealed class PlugInFunctionPrimitive : FunctionPrimitive
{
    internal PlugInFunctionPrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet)
    {
        int pluginIndex = dataSet._dicValue._operatorPriority;
        _cmd = dataSet._parent!.AddCmd(pluginIndex, Value);
    }

    private readonly Cmd? _cmd;

    internal override string Render(RenderContext context) =>
        _cmd == null ? DefaultName : _cmd.FunctionName;
}

internal sealed class ComFunctionPrimitive : FunctionPrimitive
{
    internal ComFunctionPrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet)
    {
        _func = dataSet._parent!.GetFunction(Value - 0x1000);
        _func?.MarkUsedAt(TokenOffset);
    }

    private readonly Function? _func;

    internal override string Render(RenderContext context) =>
        _func == null ? DefaultName : _func.FunctionName ?? DefaultName;
}
