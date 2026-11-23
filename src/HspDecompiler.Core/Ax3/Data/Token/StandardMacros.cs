using System.Collections.Generic;
using HspDecompiler.Core.Ax3.Data.Primitive;
using HspDecompiler.Core.Releases;

namespace HspDecompiler.Core.Ax3.Data.Token;

// The hspdef.as macros that expand to a call of a built-in function, such as "dir_exe" for
// "dirinfo(1)". hspdef.as gained macros over the releases, so each one carries the release that
// added it, and is written only when every release that could have compiled the AX has it.
//
// 組み込み関数の呼び出しに展開される hspdef.as のマクロ。"dirinfo(1)" に対する "dir_exe" など。
// hspdef.as はリリースごとにマクロが増えたため、それぞれにそれを加えたリリースを付け、AX を
// コンパイルできたすべてのリリースが持つ時だけ書く。
internal static class StandardMacros
{
    private static readonly CompilerRelease s_any = CompilerReleases.Hsp30;

    private static readonly string[] s_ginfo =
    [
        "ginfo_mx",
        "ginfo_my",
        "ginfo_act",
        "ginfo_sel",
        "ginfo_wx1",
        "ginfo_wy1",
        "ginfo_wx2",
        "ginfo_wy2",
        "ginfo_vx",
        "ginfo_vy",
        "ginfo_sizex",
        "ginfo_sizey",
        "ginfo_winx",
        "ginfo_winy",
        "ginfo_mesx",
        "ginfo_mesy",
        "ginfo_r",
        "ginfo_g",
        "ginfo_b",
        "ginfo_paluse",
        "ginfo_dispx",
        "ginfo_dispy",
        "ginfo_cx",
        "ginfo_cy",
        "ginfo_intid",
        "ginfo_newid",
        "ginfo_sx",
        "ginfo_sy",
    ];

    private static readonly string[] s_sensors =
    [
        "ginfo_accelx",
        "ginfo_accely",
        "ginfo_accelz",
        "ginfo_gyrox",
        "ginfo_gyroy",
        "ginfo_gyroz",
    ];

    private static readonly Dictionary<
        (string Function, int Argument),
        (string Name, CompilerRelease Since)
    > s_calls = Build();

    private static readonly string[] s_objinfo = ["objinfo_mode", "objinfo_bmscr", "objinfo_hwnd"];

    private static Dictionary<(string, int), (string, CompilerRelease)> Build()
    {
        var calls = new Dictionary<(string, int), (string, CompilerRelease)>
        {
            [("noteinfo", 0)] = ("notemax", s_any),
            [("noteinfo", 1)] = ("notesize", s_any),
            [("dirinfo", 0)] = ("dir_cur", s_any),
            [("dirinfo", 1)] = ("dir_exe", s_any),
            [("dirinfo", 2)] = ("dir_win", s_any),
            [("dirinfo", 3)] = ("dir_sys", s_any),
            [("dirinfo", 4)] = ("dir_cmdline", s_any),
            [("dirinfo", 5)] = ("dir_tv", CompilerReleases.Hsp331),
            [("dirinfo", 0x10000)] = ("dir_desktop", s_any),
            [("dirinfo", 0x10005)] = ("dir_mydoc", s_any),
        };
        for (int i = 0; i < s_ginfo.Length; i++)
        {
            calls[("ginfo", i)] = (
                s_ginfo[i],
                s_ginfo[i] == "ginfo_newid" ? CompilerReleases.Hsp32 : s_any
            );
        }

        for (int i = 0; i < s_sensors.Length; i++)
        {
            calls[("ginfo", 256 + i)] = (s_sensors[i], CompilerReleases.Hsp35);
        }

        return calls;
    }

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
            && s_calls.TryGetValue((name, argument), out (string Name, CompilerRelease Since) macro)
            && context.Release.From(macro.Since)
            ? macro.Name
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
