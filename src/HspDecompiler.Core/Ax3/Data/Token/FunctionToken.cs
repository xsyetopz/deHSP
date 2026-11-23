using HspDecompiler.Core.Ax3.Data.Primitive;

namespace HspDecompiler.Core.Ax3.Data.Token;

internal sealed class FunctionToken : OperandToken
{
    internal FunctionToken(FunctionPrimitive token)
    {
        _primitive = token;
    }

    internal FunctionToken(FunctionPrimitive token, ArgumentToken theArg)
    {
        _primitive = token;
        _arg = theArg;
    }

    private readonly FunctionPrimitive? _primitive;

    internal FunctionPrimitive Primitive => _primitive!;

    private readonly ArgumentToken? _arg;

    internal override int TokenOffset => _primitive!.TokenOffset;

    internal override string Render(RenderContext context) =>
        _arg == null ? _primitive!.Render(context)
        : context.Style.StandardMacros
        && StandardMacros.Spell(_primitive!, _arg, context) is { } macro
            ? macro
        : _primitive!.Render(context) + _arg.Render(context);

    internal ArgumentToken? Arguments => _arg;

    internal override int Priority(RenderContext context) => 100;

    internal override void CheckLabel() => _arg?.CheckLabel();

    internal override bool CheckRpn() => _arg == null || _arg.CheckRpn();
}
