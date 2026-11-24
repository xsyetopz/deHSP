using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Ax3.Data.Analyzer;
using HspDecompiler.Core.Keywords;
using HspDecompiler.Core.Releases;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax3.Analysis;

// The lexer names each code before the release is inferred, from the releases of the format
// word. A code whose name changed between those releases, such as 0x0F 0x1E (resume up to 3.6,
// varprop from 3.7), is named again once the inferred release is known.
//
// 字句解析はリリースを推定する前に、形式のワードのリリースから各コードに名前を付ける。その
// リリースの間で名前が変わったコード、例えば 0x0F 0x1E(3.6 までは resume、3.7 からは
// varprop)には、推定したリリースが分かった後で名前を付け直す。
//
// When the candidates still disagree, the newest candidate's name is written and a warning names
// the others, since only --compiler can settle what the AX does not show.
//
// 候補の間でまだ名前が食い違う時は、最も新しい候補の名前を書き、他の名前を警告で示す。AX が
// 示さないことを決められるのは --compiler だけであるため。
internal static class KeywordNames
{
    private static readonly CompositeFormat s_ambiguousFormat = CompositeFormat.Parse(
        Strings.KeywordNameAmbiguous
    );

    internal static void Apply(
        TokenCollection stream,
        KeywordDictionary dictionary,
        ReleaseProfile release,
        IDecompilerLogger logger
    )
    {
        var warned = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (PrimitiveToken token in stream.Primitives)
        {
            if (
                !dictionary.HasVariants(token.Type, token.Value)
                || dictionary.NameOf(token.Type, token.Value, release) is not { } name
            )
            {
                continue;
            }

            token.Rename(name);
            string readings = Readings(dictionary, token, release);
            if (readings.Contains(',', System.StringComparison.Ordinal) && warned.Add(name))
            {
                logger.Warning(
                    string.Format(CultureInfo.InvariantCulture, s_ambiguousFormat, name, readings)
                );
            }
        }
    }

    // Each name the code has among the candidates, with the releases that use it, such as
    // "resume (3.6), varprop (3.7-3.8β2)".
    //
    // 候補の間でコードが持つ各名前と、それを使うリリース。例えば
    // "resume (3.6), varprop (3.7-3.8β2)"。
    private static string Readings(
        KeywordDictionary dictionary,
        PrimitiveToken token,
        ReleaseProfile release
    ) =>
        string.Join(
            ", ",
            release
                .Candidates.GroupBy(candidate =>
                    dictionary.NameOf(token.Type, token.Value, new ReleaseProfile([candidate]))
                )
                .Select(group => group.Key + " (" + new ReleaseProfile([.. group]) + ")")
        );
}
