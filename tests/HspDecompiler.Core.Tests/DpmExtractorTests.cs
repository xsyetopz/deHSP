using System;
using System.IO;
using HspDecompiler.Core.DpmToAx;
using HspDecompiler.Core.Encoding;
using Xunit;

namespace HspDecompiler.Core.Tests;

public class DpmExtractorTests
{
    [Fact]
    public void FromBinaryReaderValidDpmHeader()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // One unencrypted 100-byte file: header, a 32-byte entry, then the data at offset 48.
        //
        // 暗号化されていない 100 バイトのファイル 1 つ。ヘッダー、32 バイトのエントリ、
        // オフセット 48 からのデータの順に並ぶ。
        bw.Write("DPMX"u8.ToArray());
        bw.Write(0);
        bw.Write(1);
        bw.Write(0);

        byte[] name = new byte[16];
        "test.ax"u8.CopyTo(name);
        bw.Write(name);
        bw.Write(0x12345678);
        bw.Write(0);
        bw.Write(0);
        bw.Write(100);

        bw.Write(new byte[100]);
        bw.Flush();

        ms.Seek(0, SeekOrigin.Begin);
        using var br = new BinaryReader(ms, ShiftJisHelper.Encoding);
        var extractor = DpmExtractor.FromBinaryReader(br);

        Assert.NotNull(extractor);
        Assert.Single(extractor.FileList);
        Assert.Equal("test.ax", extractor.FileList[0].FileName);
        Assert.False(extractor.FileList[0].IsEncrypted);
        Assert.Equal(100, extractor.FileList[0].FileSize);
        Assert.Equal(0x12345678, extractor.FileList[0].Unknown);
    }

    [Fact]
    public void FromBinaryReaderFindsDpmAfterPackerHeader()
    {
        const int peOffset = 0x40;
        const int sectionTable = peOffset + 4 + 20 + 0xE0;
        const int dpmOffset = 0x1200;
        byte[] exe = new byte[dpmOffset];
        "MZ"u8.CopyTo(exe);
        BitConverter.GetBytes(peOffset).CopyTo(exe, 0x3C);
        "PE\0\0"u8.CopyTo(exe.AsSpan(peOffset));
        BitConverter.GetBytes((ushort)1).CopyTo(exe, peOffset + 6);
        BitConverter.GetBytes((ushort)0xE0).CopyTo(exe, peOffset + 20);
        "UPX1\0\0\0\0"u8.CopyTo(exe.AsSpan(sectionTable));
        BitConverter.GetBytes(0x1000).CopyTo(exe, sectionTable + 16);
        BitConverter.GetBytes(0x200).CopyTo(exe, sectionTable + 20);
        "3.04\0UPX!"u8.CopyTo(exe.AsSpan(sectionTable + 40));

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(exe);
        bw.Write("DPMX"u8.ToArray());
        bw.Write(0);
        bw.Write(1);
        bw.Write(0);
        byte[] name = new byte[16];
        "start.ax"u8.CopyTo(name);
        bw.Write(name);
        bw.Write(-1);
        bw.Write(0);
        bw.Write(0);
        bw.Write(4);
        bw.Write(new byte[4]);
        bw.Flush();
        ms.Seek(0, SeekOrigin.Begin);
        using var br = new BinaryReader(ms, ShiftJisHelper.Encoding);

        var extractor = DpmExtractor.FromBinaryReader(br);

        Assert.NotNull(extractor);
        Assert.Equal("start.ax", Assert.Single(extractor.FileList).FileName);
    }

    [Fact]
    public void FromBinaryReaderInvalidMagicReturnsNull()
    {
        byte[] data = new byte[16];
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms, ShiftJisHelper.Encoding);
        var extractor = DpmExtractor.FromBinaryReader(br);
        Assert.Null(extractor);
    }

    [Fact]
    public void FromBinaryReaderDpm2RejectsNegativeDataOffset()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // The entry's data offset of -1 points before the data section.
        //
        // エントリのデータ位置 -1 はデータ部より前を指す。
        bw.Write("DPM2"u8.ToArray());
        bw.Write(1);
        bw.Write(64);
        bw.Write(72);
        bw.Write(8);
        bw.Write(0);
        bw.Write(0);
        bw.Write(0);

        bw.Write(0);
        bw.Write(0);
        bw.Write(1);
        bw.Write(0);
        bw.Write(-1);
        bw.Write(0);
        bw.Write(0);
        bw.Write(0);

        bw.Write("file.ax\0"u8.ToArray());
        bw.Write(new byte[10]);
        bw.Flush();

        ms.Seek(0, SeekOrigin.Begin);
        using var br = new BinaryReader(ms, ShiftJisHelper.Encoding);
        var extractor = DpmExtractor.FromBinaryReader(br);

        Assert.Null(extractor);
    }

    [Fact]
    public void DpmFileEntryIsEncryptedWhenKeyNonZero()
    {
        var entry = new DpmFileEntry { EncryptionKey = 0x1234 };
        Assert.True(entry.IsEncrypted);
    }

    [Fact]
    public void DpmFileEntryNotEncryptedWhenKeyZero()
    {
        var entry = new DpmFileEntry { EncryptionKey = 0 };
        Assert.False(entry.IsEncrypted);
    }
}
