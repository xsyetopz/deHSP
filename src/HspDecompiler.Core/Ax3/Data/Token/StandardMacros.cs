using System.Collections.Generic;
using HspDecompiler.Core.Ax3.Data.Primitive;

namespace HspDecompiler.Core.Ax3.Data.Token;

// The hspdef.as macros that expand to a call of a built-in function, such as "dir_exe" for
// "dirinfo(1)". hspdef.as gained macros over the releases, so each one carries the first AX
// format version whose compiler always has it. The 0x301 format covers 3.1 to 3.4, and
// ginfo_newid (3.2) and dir_tv (3.31) arrived inside that range, so they wait for 0x350.
//
// 組み込み関数の呼び出しに展開される hspdef.as のマクロ。"dirinfo(1)" に対する "dir_exe" など。
// hspdef.as はリリースごとにマクロが増えたため、それぞれにそのマクロを必ず持つコンパイラの
// 最初の AX 形式バージョンを付ける。0x301 形式は 3.1〜3.4 にまたがり、ginfo_newid(3.2)と
// dir_tv(3.31)はその途中で加わったため、0x350 からとする。
internal static class StandardMacros
{
    private const uint AnyVersion = 0;

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
        (string Name, uint Since)
    > s_calls = Build();

    private static readonly string[] s_objinfo = ["objinfo_mode", "objinfo_bmscr", "objinfo_hwnd"];

    private static Dictionary<(string, int), (string, uint)> Build()
    {
        var calls = new Dictionary<(string, int), (string, uint)>
        {
            [("noteinfo", 0)] = ("notemax", AnyVersion),
            [("noteinfo", 1)] = ("notesize", AnyVersion),
            [("dirinfo", 0)] = ("dir_cur", AnyVersion),
            [("dirinfo", 1)] = ("dir_exe", AnyVersion),
            [("dirinfo", 2)] = ("dir_win", AnyVersion),
            [("dirinfo", 3)] = ("dir_sys", AnyVersion),
            [("dirinfo", 4)] = ("dir_cmdline", AnyVersion),
            [("dirinfo", 5)] = ("dir_tv", AxHeader.Version35),
            [("dirinfo", 0x10000)] = ("dir_desktop", AnyVersion),
            [("dirinfo", 0x10005)] = ("dir_mydoc", AnyVersion),
        };
        for (int i = 0; i < s_ginfo.Length; i++)
        {
            calls[("ginfo", i)] = (
                s_ginfo[i],
                s_ginfo[i] == "ginfo_newid" ? AxHeader.Version35 : AnyVersion
            );
        }

        for (int i = 0; i < s_sensors.Length; i++)
        {
            calls[("ginfo", 256 + i)] = (s_sensors[i], AxHeader.Version35);
        }

        return calls;
    }

    // The macro spelling of a call, or null when no macro expands to it.
    //
    // 呼び出しのマクロとしての書き方。展開元のマクロが無ければ null。
    internal static string? Spell(FunctionPrimitive function, ArgumentToken arguments)
    {
        if (function is not HspFunctionPrimitive || !arguments.HasBrackets)
        {
            return null;
        }

        string name = function.ToString();
        List<ExpressionToken> exps = arguments.Exps;
        return
            exps.Count == 1
            && exps[0].Literal?.Integer is { } argument
            && s_calls.TryGetValue((name, argument), out (string Name, uint Since) macro)
            && function.FormatVersion >= macro.Since
            ? macro.Name
            : Objinfo(name, exps);
    }

    private static string? Objinfo(string name, List<ExpressionToken> exps) =>
        name == "objinfo"
        && exps.Count == 2
        && exps[1].Literal?.Integer is { } mode
        && mode >= 0
        && mode < s_objinfo.Length
            ? s_objinfo[mode] + "(" + exps[0] + ")"
            : null;
}
