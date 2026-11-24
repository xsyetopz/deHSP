using System.Globalization;
using System.Text;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax2;
using HspDecompiler.Core.Ax3;
using HspDecompiler.Core.Keywords;
using HspDecompiler.Core.Releases;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Pipeline;

// What one decode reads besides the image: the keyword dictionary, the source style, the
// caller's common files and the compiler release the caller says wrote the AX. One run builds
// it once and passes it down, so no stage keeps per-run state in a field.
//
// 1 回の復元がイメージ以外に読むもの。キーワード辞書、ソースの書き方、呼び出し側の共通
// ファイル、呼び出し側が AX を書いたと言うコンパイラのリリース。1 回の実行で一度作って下へ
// 渡すため、どの段も実行ごとの状態をフィールドに持たない。
internal sealed record DecodeSettings(
    KeywordDictionary Dictionary,
    SourceStyle Style,
    ICommonFiles? CommonFiles = null,
    string? CompilerHint = null
)
{
    private static readonly CompositeFormat s_compilerHintIgnoredFormat = CompositeFormat.Parse(
        Strings.CompilerHintIgnored
    );

    internal ISourceDecoder DecoderFor(AxFormat format) =>
        format == AxFormat.Ax2 ? new Ax2Decoder(this) : new Ax3Decoder(this);

    // The release the evidence allows, pinned to the caller's hint when the evidence leaves the
    // hint a candidate. Both decoders ask this, so a hint means the same for AX2 and AX3.
    //
    // 証拠が許すリリース。呼び出し側の指定が候補に残る時はそれに絞る。両方のデコーダーがこれを
    // 尋ねるため、指定は AX2 でも AX3 でも同じ意味を持つ。
    internal ReleaseProfile ReleaseFor(AxEvidence evidence, IDecompilerLogger logger)
    {
        var inferred = ReleaseProfile.Infer(evidence);
        if (CompilerHint is not { } hint)
        {
            return inferred;
        }

        if (inferred.Pin(hint) is { } pinned)
        {
            return pinned;
        }

        logger.Warning(
            string.Format(CultureInfo.InvariantCulture, s_compilerHintIgnoredFormat, hint, inferred)
        );
        return inferred;
    }
}
