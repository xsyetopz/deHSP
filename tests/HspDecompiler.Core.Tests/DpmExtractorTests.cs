using System;
using System.IO;
using System.Text;
using Xunit;
using HspDecompiler.Core.DpmToAx;
using HspDecompiler.Core.Encoding;

namespace HspDecompiler.Core.Tests
{
    public class DpmExtractorTests
    {
        [Fact]
        public void Test_FromBinaryReader_Valid_Dpm_Header()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // 16-byte header: magic, unknown, fileCount, unknown
            bw.Write(new byte[] { 0x44, 0x50, 0x4D, 0x58 }); // DPMX
            bw.Write(0);  // unknown
            bw.Write(1);  // fileCount = 1
            bw.Write(0);  // unknown

            // 32-byte file entry: 16-byte name + 4 int32 fields
            byte[] name = new byte[16];
            System.Text.Encoding.ASCII.GetBytes("test.ax").CopyTo(name, 0);
            bw.Write(name);
            bw.Write(0);    // unknown
            bw.Write(0);    // EncryptionKey = 0 (not encrypted)
            bw.Write(0);    // FileOffset = 0
            bw.Write(100);  // FileSize = 100

            // file data (fileOffsetStart=48, fileOffset=0 => seek to 48; data must be 100 bytes)
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
        }

        [Fact]
        public void Test_FromBinaryReader_Invalid_Magic_Returns_Null()
        {
            var data = new byte[16];
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms, ShiftJisHelper.Encoding);
            var extractor = DpmExtractor.FromBinaryReader(br);
            Assert.Null(extractor);
        }

        [Fact]
        public void Test_DpmFileEntry_IsEncrypted_When_Key_NonZero()
        {
            var entry = new DpmFileEntry { EncryptionKey = 0x1234 };
            Assert.True(entry.IsEncrypted);
        }

        [Fact]
        public void Test_DpmFileEntry_Not_Encrypted_When_Key_Zero()
        {
            var entry = new DpmFileEntry { EncryptionKey = 0 };
            Assert.False(entry.IsEncrypted);
        }
    }
}
