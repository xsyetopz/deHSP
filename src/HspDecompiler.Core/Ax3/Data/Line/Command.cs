using HspDecompiler.Core.Ax3.Data.Primitive;
using HspDecompiler.Core.Ax3.Data.Token;
using HspDecompiler.Core.Pipeline;

namespace HspDecompiler.Core.Ax3.Data.Line;

internal sealed class Command : LogicalLine
{
    internal Command(FunctionToken function)
    {
        _function = function;
        if (function.Primitive.Style.HexLiterals == HexLiterals.Poke)
        {
            MarkPokeValue(function);
        }
    }

    private static readonly string[] s_pokes = ["poke", "wpoke", "lpoke"];

    // The value a poke writes is a byte pattern; one at 128 or more is past ASCII, where a
    // source usually spells it in hex ("poke buf, 0, 0xEF").
    //
    // poke が書き込む値はバイトのパターンである。128 以上は ASCII の外で、ソースでは普通
    // 16 進数で書く("poke buf, 0, 0xEF")。
    private static void MarkPokeValue(FunctionToken function)
    {
        if (
            function.Primitive is HspFunctionPrimitive primitive
            && System.Array.IndexOf(s_pokes, primitive.ToString()) >= 0
            && function.Arguments?.Exps is { Count: >= 3 } exps
            && exps[2].Literal is { Integer: >= 128 } value
        )
        {
            value.WriteInHex();
        }
    }

    private readonly FunctionToken? _function;

    internal override bool TabIncrement =>
        (_function!.Primitive.CodeExtraFlags & HspCodeExtraOptions.AddTab)
        == HspCodeExtraOptions.AddTab;
    internal override bool TabDecrement =>
        (_function!.Primitive.CodeExtraFlags & HspCodeExtraOptions.RemoveTab)
        == HspCodeExtraOptions.RemoveTab;
    internal override bool HasFlagGhostGoto =>
        (_function!.Primitive.CodeExtraFlags & HspCodeExtraOptions.HasGhostGoto)
        == HspCodeExtraOptions.HasGhostGoto;
    internal override bool HasFlagIsGhost =>
        (_function!.Primitive.CodeExtraFlags & HspCodeExtraOptions.IsGhost)
        == HspCodeExtraOptions.IsGhost;

    internal override int TokenOffset => _function!.TokenOffset;

    public override string ToString() => _function!.ToString();

    internal override void CheckLabel() => _function?.CheckLabel();

    internal override bool CheckRpn() => _function == null || _function.CheckRpn();
}
