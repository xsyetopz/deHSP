using HspDecompiler.Core.Ax3.Data.Primitive;
using HspDecompiler.Core.Ax3.Data.Token;

namespace HspDecompiler.Core.Ax3.Data.Line;

internal sealed class Command : LogicalLine
{
    internal Command(FunctionToken function)
    {
        _function = function;
        MarkPokeValue(function);
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
            && System.Array.IndexOf(s_pokes, primitive.Name) >= 0
            && function.Arguments?.Exps is { Count: >= 3 } exps
            && exps[2].Literal is { Integer: >= 128 } value
        )
        {
            value.MarkPokeByte();
        }
    }

    private readonly FunctionToken? _function;

    // TYPE_PROGCMD with value 0 is goto (hsp3code.h), whatever name the dictionary gives it.
    //
    // TYPE_PROGCMD の値 0 は goto である(hsp3code.h)。辞書が付ける名前には依らない。
    private const int ProgramCommandType = 0x0F;
    private const int GotoValue = 0;

    // True for "goto" with the label as its only argument, as hspcmp writes for #module.
    //
    // ラベルだけを引数に持つ "goto" なら真。hspcmp は #module にこれを書く。
    internal bool IsGotoTo(PP.Label label) =>
        _function is { Arguments.Exps: [{ Literal.Label: { } target }] } function
        && function.Primitive.IsCode(ProgramCommandType, GotoValue)
        && ReferenceEquals(target, label);

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

    internal override string Render(RenderContext context) => _function!.Render(context);

    internal override void CheckLabel() => _function?.CheckLabel();

    internal override bool CheckRpn() => _function == null || _function.CheckRpn();
}
