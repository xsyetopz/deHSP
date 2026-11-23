using System.Collections.Generic;

namespace HspDecompiler.Core.Ax3;

// Turns the HSPHED bootoption word back into the directives that set it. The bit values come
// from hsp3struct.h and the directive names from hspcmp's token.cpp (#bootopt, #cmpopt).
// DEBUGWIN comes from the -w switch rather than the source, so no directive can restore it.
//
// HSPHED の bootoption ワードを、それを立てたディレクティブに戻す。ビット値は hsp3struct.h、
// ディレクティブ名は hspcmp の token.cpp(#bootopt、#cmpopt)による。DEBUGWIN はソースでは
// なく -w スイッチから来るため、どのディレクティブでも復元できない。
internal static class BootOptionDirectives
{
    internal const uint NoMmTimer = 0x2000;
    internal const uint NoGdip = 0x4000;
    internal const uint Float32 = 0x8000;
    internal const uint OrgRnd = 0x10000;
    internal const uint Utf8 = 0x20000;
    internal const uint Hsp64 = 0x40000;
    internal const uint IoResume = 0x80000;

    internal const string Utf8StringsDirective = "#cmpopt utf8 1";

    private static readonly (uint Bit, string Directive)[] s_plainOptions =
    [
        (Hsp64, "#bootopt hsp64 1"),
        (NoGdip, "#bootopt nogdip 1"),
        (Float32, "#bootopt float32 1"),
        (OrgRnd, "#bootopt orgrnd 1"),
        (IoResume, "#bootopt ioresume 1"),
    ];

    internal static List<string> Build(uint bootOption, bool utf8Strings, bool usesAwait)
    {
        var lines = new List<string>();

        // `#cmpopt utf8 1` converts the script strings and also sets the UTF-8 bit, so the bit
        // needs its own #bootopt only when the strings stayed Shift_JIS.
        //
        // `#cmpopt utf8 1` はスクリプト文字列を変換し UTF-8 ビットも立てるため、文字列が
        // Shift_JIS のままの時だけビットに #bootopt を別に書く。
        if (utf8Strings)
        {
            lines.Add(Utf8StringsDirective);
        }
        else if ((bootOption & Utf8) != 0)
        {
            lines.Add("#bootopt utf8 1");
        }

        foreach ((uint bit, string directive) in s_plainOptions)
        {
            if ((bootOption & bit) != 0)
            {
                lines.Add(directive);
            }
        }

        // hspcmp 3.5, 3.7 and 3.8 beta 2 set NOMMTIMER by themselves when the code has no await.
        // The bit only changes how await waits, so it matters only beside an await, and only a
        // set bit there needs `#bootopt notimer 1`. A clear bit without await comes from an older
        // compiler and changes nothing at run time. Only hspcmp 3.5 honours #bootopt notimer;
        // 3.7 and 3.8 beta 2 always follow the await rule.
        //
        // hspcmp 3.5・3.7・3.8 beta 2 はコードに await が無ければ自動で NOMMTIMER を立てる。
        // このビットは await の待ち方だけを変えるため await がある時だけ意味を持ち、そこで
        // ビットが立っていれば `#bootopt notimer 1` を書く。await が無いのにビットが無い AX は
        // 古いコンパイラによるもので実行時の違いは無い。#bootopt notimer に従うのは hspcmp 3.5
        // だけで、3.7 と 3.8 beta 2 は常に await の有無で決める。
        if (usesAwait && (bootOption & NoMmTimer) != 0)
        {
            lines.Add("#bootopt notimer 1");
        }

        return lines;
    }
}
