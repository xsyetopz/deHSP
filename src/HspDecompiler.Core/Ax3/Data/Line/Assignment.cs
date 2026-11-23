using HspDecompiler.Core.Ax3.Data.Token;

namespace HspDecompiler.Core.Ax3.Data.Line;

internal sealed class Assignment : LogicalLine
{
    internal Assignment(VariableToken theVar, OperatorToken theOp)
    {
        _var = theVar;
        _op = theOp;
    }

    internal Assignment(VariableToken theVar, OperatorToken theOp, ArgumentToken theArg)
    {
        _var = theVar;
        _op = theOp;
        _arg = theArg;
    }

    private readonly VariableToken? _var;
    private readonly OperatorToken? _op;
    private readonly ArgumentToken? _arg;

    internal override int TokenOffset => _var == null ? -1 : _var.TokenOffset;

    internal override string Render(RenderContext context)
    {
        string target = _var!.Render(context);
        return _arg != null
            ? target + " " + _op!.Render(context, true, true) + _arg.Render(context)
            : target + _op!.Render(context, true, false);
    }

    internal override void CheckLabel()
    {
        _var?.CheckLabel();

        _op?.CheckLabel();

        _arg?.CheckLabel();
    }

    internal override bool CheckRpn() =>
        (_var == null || _var.CheckRpn()) && (_arg == null || _arg.CheckRpn());
}
