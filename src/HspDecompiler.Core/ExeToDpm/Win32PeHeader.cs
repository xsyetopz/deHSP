using System.IO;

namespace HspDecompiler.Core.ExeToDpm;

// Reads only the PE fields that locate the end of the executable image: the DOS header's
// e_lfanew, the file header's section count and optional header size, and each section's raw
// data range. Offsets follow the IMAGE_* structures in winnt.h.
//
// 実行イメージの終端を求めるのに必要な PE のフィールドだけを読む。DOS ヘッダーの e_lfanew、
// ファイルヘッダーのセクション数とオプションヘッダーのサイズ、各セクションの生データ範囲である。
// オフセットは winnt.h の IMAGE_* 構造体に従う。
internal sealed class Win32PeHeader
{
    // "MZ" and "PE\0\0" read as little-endian values.
    //
    // リトルエンディアンで読んだ "MZ" と "PE\0\0"。
    private const ushort DosSignature = 0x5A4D;
    private const uint NtSignature = 0x4550;
    private const int LfanewOffset = 0x3C;
    private const int FileHeaderSize = 20;
    private const int SectionHeaderSize = 40;
    private const int SectionRawDataOffset = 16;

    private Win32PeHeader(long endOfExecutableRegion)
    {
        EndOfExecutableRegion = endOfExecutableRegion;
    }

    internal long EndOfExecutableRegion { get; }

    internal static Win32PeHeader? FromBinaryReader(BinaryReader reader)
    {
        try
        {
            Stream stream = reader.BaseStream;
            long start = stream.Position;
            if (stream.Length - start < 0x1000 || reader.ReadUInt16() != DosSignature)
            {
                return null;
            }

            stream.Seek(start + LfanewOffset, SeekOrigin.Begin);
            uint lfanew = reader.ReadUInt32();
            stream.Seek(start + lfanew, SeekOrigin.Begin);
            if (lfanew == 0 || reader.ReadUInt32() != NtSignature)
            {
                return null;
            }

            stream.Seek(2, SeekOrigin.Current);
            ushort sectionCount = reader.ReadUInt16();
            stream.Seek(12, SeekOrigin.Current);
            ushort optionalHeaderSize = reader.ReadUInt16();

            // Packers such as UPX write their own header right after the section table, so the
            // table is read by the count in the file header instead of up to an all-zero entry.
            //
            // UPX などのパッカーはセクション表の直後に独自のヘッダーを書くため、全ゼロの項目まで
            // ではなく、ファイルヘッダーのセクション数だけ表を読む。
            long table = start + lfanew + 4 + FileHeaderSize + optionalHeaderSize;
            long end = -1;
            for (int i = 0; i < sectionCount; i++)
            {
                stream.Seek(
                    table + (i * SectionHeaderSize) + SectionRawDataOffset,
                    SeekOrigin.Begin
                );
                uint size = reader.ReadUInt32();
                uint pointer = reader.ReadUInt32();
                end = long.Max(end, (long)pointer + size);
            }
            return new Win32PeHeader(end);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
