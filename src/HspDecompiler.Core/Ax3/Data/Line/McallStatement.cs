using System.Text;
using HspDecompiler.Core.Ax3.Data.Primitive;
using HspDecompiler.Core.Ax3.Data.Token;

namespace HspDecompiler.Core.Ax3.Data.Line;

internal class McallStatement : LogicalLine
{
    internal McallStatement(
        McallFunctionPrimitive theToken,
        VariablePrimitive var,
        ExpressionToken exp,
        ArgumentToken? arg
    )
    {
        _token = theToken;
        _var = var;
        _exp = exp;
        _arg = arg;
    }

    private readonly McallFunctionPrimitive? _token;
    private readonly VariablePrimitive? _var;
    private readonly ExpressionToken? _exp;
    private readonly ArgumentToken? _arg;

    internal override int TokenOffset => _token!.TokenOffset;

    private string RenderFunctionStyle(RenderContext context)
    {
        if (_arg == null)
        {
            return _token!.Render(context);
        }

        var builder = new StringBuilder(_token!.Render(context));
        if (_var != null)
        {
            builder.Append(' ');
            builder.Append(_var.Render(context));
            if (_exp != null)
            {
                builder.Append(" ,");
                builder.Append(_var.Render(context));
            }
        }

        builder.Append(_arg.Render(context));
        return builder.ToString();
    }

    internal override string Render(RenderContext context) =>
        (_var == null) || (_exp == null) || (_arg == null)
            ? RenderFunctionStyle(context)
            : _var.Render(context) + "->" + _exp.Render(context) + _arg.Render(context, true);

    internal override void CheckLabel()
    {
        _exp?.CheckLabel();

        _arg?.CheckLabel();
    }

    internal override bool CheckRpn()
    {
        bool ret = true;
        if (_exp != null)
        {
            ret &= _exp.CheckRpn();
        }

        if (_arg != null)
        {
            ret &= _arg.CheckRpn();
        }

        return ret;
    }
}
