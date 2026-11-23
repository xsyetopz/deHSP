using System;
using System.IO;

namespace HspDecompiler.Core.Pipeline;

// The two AX layouts: hspcmp 2.x writes "HSP2" images and 3.x writes "HSP3" ones.
//
// 2 つの AX の形式。hspcmp 2.x は "HSP2"、3.x は "HSP3" のイメージを書く。
internal enum AxFormat
{
    Ax2,
    Ax3,
}

// Reads the four-byte magic at the start of an input, the one place the pipeline tells AX
// images, executables and archives apart.
//
// 入力の先頭 4 バイトのマジックを読む。パイプラインが AX イメージ・実行ファイル・アーカイブを
// 見分ける唯一の場所。
internal static class AxFormats
{
    internal const string Ax2Magic = "HSP2";
    internal const string Ax3Magic = "HSP3";
    private const int MagicLength = 4;

    // The magic at the reader's position; the position is left where it was.
    //
    // 読み取り位置にあるマジック。位置は元のまま残す。
    internal static string Peek(BinaryReader reader)
    {
        long start = reader.BaseStream.Position;
        string magic = new(reader.ReadChars(MagicLength));
        reader.BaseStream.Seek(start, SeekOrigin.Begin);
        return magic;
    }

    internal static AxFormat? Of(string magic) =>
        magic.Equals(Ax2Magic, StringComparison.Ordinal) ? AxFormat.Ax2
        : magic.Equals(Ax3Magic, StringComparison.Ordinal) ? AxFormat.Ax3
        : null;

    internal static AxFormat? Detect(BinaryReader reader) => Of(Peek(reader));

    // An executable or a DPM archive, which holds AX images rather than being one.
    //
    // AX イメージそのものではなく、それを収める実行ファイルか DPM アーカイブ。
    internal static bool IsContainer(string magic) =>
        magic.StartsWith("MZ", StringComparison.Ordinal)
        || magic.StartsWith("DPM", StringComparison.Ordinal);

    internal static string SourceExtension(AxFormat? format) =>
        format == AxFormat.Ax2 ? ".as" : ".hsp";
}
