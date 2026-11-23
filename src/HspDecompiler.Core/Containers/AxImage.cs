using System;
using System.IO;
using HspDecompiler.Core.Encoding;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Pipeline;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Containers;

// One AX image in memory, wherever it came from: a plain .ax file, a DPMX or DPM2 entry, or the
// archive inside an executable. Every container yields this one value, so a container quirk is
// handled before the decoder and the decoder never reads a file.
//
// メモリ上の 1 つの AX イメージ。出どころ(素の .ax ファイル、DPMX・DPM2 のエントリ、実行
// ファイル内のアーカイブ)を問わない。すべてのコンテナがこの 1 つの値を返すため、コンテナの
// 癖はデコーダーの前で片付き、デコーダーはファイルを読まない。
internal sealed record AxImage(string Name, ReadOnlyMemory<byte> Bytes, AxFormat Format)
{
    // The image named by the AX magic at the start of the bytes.
    //
    // バイト列の先頭にある AX のマジックで決まるイメージ。
    internal static AxImage Of(string name, byte[] bytes) =>
        FormatOf(bytes) is { } format
            ? new AxImage(name, bytes, format)
            : throw new HspDecoderException(Strings.NotHsp2OrHsp3);

    internal static AxFormat? FormatOf(byte[] bytes)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes), ShiftJisHelper.Encoding);
        return bytes.Length < 4 ? null : AxFormats.Detect(reader);
    }

    // A reader over the bytes, positioned at the image's start, as the format readers take.
    //
    // 形式ごとの読み取り部が受け取る、イメージの先頭を指すバイト列のリーダー。
    internal BinaryReader Open() =>
        new(new MemoryStream(Bytes.ToArray(), false), ShiftJisHelper.Encoding);
}
