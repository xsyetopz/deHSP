using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax2.Data;
using HspDecompiler.Core.Ax2.Reading;
using HspDecompiler.Core.Ax2.Writing;
using HspDecompiler.Core.Containers;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Pipeline;
using HspDecompiler.Core.Releases;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax2;

internal sealed class Ax2Decoder(DecodeSettings settings) : ISourceDecoder
{
    public Task<DecodedSource> DecodeAsync(
        AxImage image,
        string mainFileName,
        IDecompilerLogger logger,
        IProgressReporter progress,
        CancellationToken ct
    )
    {
        try
        {
            logger.Write(Strings.AnalyzingHeader);
            using BinaryReader reader = image.Open();
            AxData data = Ax2ImageReader.Read(reader.BaseStream);
            data.Keywords = settings.Dictionary;
            data.Release = ReleaseProfile.ForFormat(data.Header.Version);
            HashSet<(int Type, int Value)> builtins = BuiltinsOf(data);
            ReleaseProfile release = settings.ReleaseFor(EvidenceOf(data, builtins), logger);
            data.Release = release;
            WarnAmbiguousNames(data, builtins, logger);

            logger.Write(Strings.AnalyzingData);
            return Task.FromResult(Ax2Emitter.Emit(data, release, mainFileName));
        }
        catch (System.SystemException e)
        {
            throw new HspDecoderException("AxData", Strings.UnexpectedError, e);
        }
    }

    private static readonly CompositeFormat s_ambiguousFormat = CompositeFormat.Parse(
        Strings.KeywordNameAmbiguous
    );

    // An HSP 2 AX shows its format word, the built-ins it calls and whether a #deffunc took a
    // built-in's name; it has none of the 3.x line records, parameter flags or frames. The
    // built-ins leave the releases whose keyword table has each of them, as strmid does 2.4d
    // and later among the releases of word 0x203.
    //
    // HSP 2 の AX が示すのは形式のワード、呼ぶ組み込み命令、#deffunc が組み込み命令の名前を
    // 取ったかどうか。3.x の行の記録・引数の印・フレームは持たない。組み込み命令は、それぞれを
    // キーワード表に持つリリースを残す。例えば strmid はワード 0x203 のリリースのうち 2.4d 以降を
    // 残す。
    private static AxEvidence EvidenceOf(AxData data, HashSet<(int Type, int Value)> builtins)
    {
        ReleaseSpan keywords = ReleaseSpan.All;
        foreach ((int type, int value) in builtins)
        {
            if (data.Keywords.SpanOf(type, value) is { } span && span.Overlaps(data.Release))
            {
                keywords = keywords.Intersect(span);
            }
        }

        return new(data.Header.Version, 0, false)
        {
            Keywords = keywords,
            RedefinedBuiltin = data.Deffuncs.Any(d => data.IsBuiltinName(d.Name)),
        };
    }

    private static HashSet<(int Type, int Value)> BuiltinsOf(AxData data)
    {
        var builtins = new HashSet<(int Type, int Value)>();
        var cursor = new TokenCursor(data);
        cursor.SetZero();
        Token? token;
        while ((token = cursor.GetNext()) != null)
        {
            if (token.Builtin is { } code)
            {
                builtins.Add(code);
            }
        }

        return builtins;
    }

    // A code the candidates name differently, such as 0x48 0x42 (hspmode up to 2.4e, keysend
    // in 2.4f), is written with the newest candidate's name, and the warning names the others,
    // since only --compiler can settle what the AX does not show.
    //
    // 候補によって名前の違うコード、例えば 0x48 0x42(2.4e までは hspmode、2.4f では keysend)
    // は最も新しい候補の名前で書き、警告で他の名前を示す。AX が示さないことを決められるのは
    // --compiler だけであるため。
    private static void WarnAmbiguousNames(
        AxData data,
        HashSet<(int Type, int Value)> builtins,
        IDecompilerLogger logger
    )
    {
        foreach (
            (int type, int value) in builtins.Where(c => data.Keywords.HasVariants(c.Type, c.Value))
        )
        {
            var readings = data
                .Release.Candidates.GroupBy(candidate =>
                    data.Keywords.NameOf(type, value, new ReleaseProfile([candidate]))
                )
                .Where(group => group.Key != null)
                .ToList();
            if (readings.Count > 1)
            {
                string list = string.Join(
                    ", ",
                    readings.Select(group =>
                        group.Key + " (" + new ReleaseProfile([.. group]) + ")"
                    )
                );
                logger.Warning(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        s_ambiguousFormat,
                        data.Keywords.NameOf(type, value, data.Release),
                        list
                    )
                );
            }
        }
    }
}
