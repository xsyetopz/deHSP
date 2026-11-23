using HspDecompiler.Core.Ax3.Data.Primitive;

namespace HspDecompiler.Core.Ax3.Data.Token;

internal sealed class VariableToken : OperandToken
{
    internal VariableToken(VariablePrimitive var)
    {
        _primitive = var;
        _arg = null;
    }

    internal VariableToken(VariablePrimitive var, ArgumentToken theArg)
    {
        _primitive = var;
        _arg = theArg;
    }

    private readonly VariablePrimitive? _primitive;
    private readonly ArgumentToken? _arg;

    internal override int TokenOffset => _primitive == null ? -1 : _primitive.TokenOffset;

    internal override string Render(RenderContext context) =>
        _primitive!.Render(context) + (_arg?.Render(context) ?? string.Empty);

    internal override int Priority(RenderContext context) => 100;

    internal override void CheckLabel() => _arg?.CheckLabel();

    internal override bool CheckRpn() => _arg == null || _arg.CheckRpn();
}
