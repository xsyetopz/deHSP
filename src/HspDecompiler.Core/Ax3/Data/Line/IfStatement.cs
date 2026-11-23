using HspDecompiler.Core.Ax3.Data.Primitive;
using HspDecompiler.Core.Ax3.Data.Token;
using HspDecompiler.Core.Pipeline;

namespace HspDecompiler.Core.Ax3.Data.Line;

internal sealed class IfStatement : LogicalLine
{
    internal IfStatement(IfStatementPrimitive token)
    {
        _ifToken = token;
    }

    internal IfStatement(IfStatementPrimitive token, ArgumentToken theArg)
    {
        _ifToken = token;
        _arg = theArg;
    }

    private readonly IfStatementPrimitive? _ifToken;
    private readonly ArgumentToken? _arg;

    internal override int TokenOffset => _ifToken == null ? -1 : _ifToken.TokenOffset;

    internal int JumpToOffset => _ifToken!.JumpToOffset;

    internal bool IsIfStatement => _ifToken!.CodeType == HspCodeType.IfStatement;

    internal bool IsElseStatement => _ifToken!.CodeType == HspCodeType.ElseStatement;

    private bool _scopeEndIsDefined;
    internal bool ScopeEndIsDefined
    {
        get => _scopeEndIsDefined;
        set => _scopeEndIsDefined = value;
    }
    internal override bool TabIncrement => _scopeEndIsDefined;

    internal override string Render(RenderContext context)
    {
        string head = _ifToken!.Render(context);
        return _arg == null
            ? head + " {"
            : head
                + Condition(_arg.Render(context).TrimStart(' '), context.Style.IfParentheses)
                + " {";
    }

    private static string Condition(string condition, IfParentheses parentheses) =>
        parentheses switch
        {
            IfParentheses.None => " " + condition,
            IfParentheses.Tight => " (" + condition + ")",
            IfParentheses.Spaced => " ( " + condition + " )",
            _ => throw new System.ArgumentOutOfRangeException(nameof(parentheses)),
        };

    internal override void CheckLabel() => _arg?.CheckLabel();

    internal override bool CheckRpn() => _arg == null || _arg.CheckRpn();
}
