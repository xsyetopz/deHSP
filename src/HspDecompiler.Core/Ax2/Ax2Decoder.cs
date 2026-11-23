using System.IO;
using System.Linq;
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
            ReleaseProfile release = settings.ReleaseFor(EvidenceOf(data), logger);

            logger.Write(Strings.AnalyzingData);
            return Task.FromResult(Ax2Emitter.Emit(data, release, mainFileName));
        }
        catch (System.SystemException e)
        {
            throw new HspDecoderException("AxData", Strings.UnexpectedError, e);
        }
    }

    // An HSP 2 AX shows its format word and whether a #deffunc took a built-in's name; it has
    // none of the 3.x line records, parameter flags or frames.
    //
    // HSP 2 の AX が示すのは形式のワードと、#deffunc が組み込み命令の名前を取ったかどうか。
    // 3.x の行の記録・引数の印・フレームは持たない。
    private static AxEvidence EvidenceOf(AxData data) =>
        new(data.Header.Version, 0, false)
        {
            RedefinedBuiltin = data.Deffuncs.Any(d => Token.IsBuiltinName(d.Name)),
        };
}
