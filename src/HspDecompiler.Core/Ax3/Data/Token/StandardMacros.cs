using System.Collections.Generic;
using HspDecompiler.Core.Ax3.Data.Primitive;

namespace HspDecompiler.Core.Ax3.Data.Token;

// The hspdef.as macros that expand to a call of a built-in function, such as "dir_exe" for
// "dirinfo(1)". The keyword table lists the one-argument macros with the releases whose
// hspdef.as has them, and a macro is written only when every release that could have compiled
// the AX has it. objinfo's macros take the object as their argument and keep the mode out of
// it, so they are spelled here.
//
// 組み込み関数の呼び出しに展開される hspdef.as のマクロ。"dirinfo(1)" に対する "dir_exe" など。
// キーワードの表は引数 1 つのマクロを、それを hspdef.as に持つリリースと共に並べる。マクロは
// AX をコンパイルできたすべてのリリースが持つ時だけ書く。objinfo のマクロはオブジェクトを引数に
// 取りモードを含めないため、ここで書く。
internal static class StandardMacros
{
    private static readonly string[] s_objinfo = ["objinfo_mode", "objinfo_bmscr", "objinfo_hwnd"];

    // The macro spelling of a call, or null when no macro expands to it.
    //
    // 呼び出しのマクロとしての書き方。展開元のマクロが無ければ null。
    internal static string? Spell(
        FunctionPrimitive function,
        ArgumentToken arguments,
        RenderContext context
    )
    {
        if (function is not HspFunctionPrimitive || !arguments.HasBrackets)
        {
            return null;
        }

        string name = function.Name ?? string.Empty;
        List<ExpressionToken> exps = arguments.Exps;
        return
            exps.Count == 1
            && exps[0].Literal?.Integer is { } argument
            && context.Keywords.MacroFor(name, argument, context.Release) is { } macro
            ? macro
            : Objinfo(name, exps, context);
    }

    private static string? Objinfo(
        string name,
        List<ExpressionToken> exps,
        RenderContext context
    ) =>
        name == "objinfo"
        && exps.Count == 2
        && exps[1].Literal?.Integer is { } mode
        && mode >= 0
        && mode < s_objinfo.Length
            ? s_objinfo[mode] + "(" + exps[0].Render(context) + ")"
            : null;
}
