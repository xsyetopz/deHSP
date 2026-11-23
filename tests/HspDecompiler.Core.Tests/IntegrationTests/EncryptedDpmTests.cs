using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.DpmToAx.Crypto;
using HspDecompiler.Core.Pipeline;
using Xunit;

namespace HspDecompiler.Core.Tests.IntegrationTests;

public sealed class EncryptedDpmTests : IDisposable
{
    private readonly string _workDir = Path.Combine(
        Path.GetTempPath(),
        $"hsp-decompiler-test-{Guid.NewGuid():N}"
    );

    public EncryptedDpmTests()
    {
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose() => Directory.Delete(_workDir, true);

    [Theory]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, true, true, false)]
    public async Task MixedArchiveExtractsEntriesTheOptionsAllow(
        bool allowDecryption,
        bool skipEncrypted,
        bool expectPlain,
        bool expectSecret
    )
    {
        DecompilerResult result = await RunAsync(
            WriteArchive(encryptPlainEntry: false),
            allowDecryption,
            skipEncrypted
        );

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(expectPlain, File.Exists(OutputFile("plain.hsp")));
        Assert.Equal(expectSecret, File.Exists(OutputFile("secret.hsp")));
        Assert.Equal(expectSecret, File.Exists(OutputFile("secret.ax")));
    }

    [Fact]
    public async Task MixedArchiveFailsWhenEncryptedEntriesAreNeitherDecryptedNorSkipped()
    {
        DecompilerResult result = await RunAsync(
            WriteArchive(encryptPlainEntry: false),
            allowDecryption: false,
            skipEncrypted: false
        );

        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
        Assert.False(Directory.Exists(OutputDirectory));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task EncryptedOnlyArchiveFailsUnlessDecrypted(
        bool allowDecryption,
        bool skipEncrypted
    )
    {
        DecompilerResult result = await RunAsync(
            WriteArchive(encryptPlainEntry: true),
            allowDecryption,
            skipEncrypted
        );

        Assert.False(result.Success);
        Assert.False(Directory.Exists(OutputDirectory));
    }

    [Fact]
    public async Task EncryptedOnlyArchiveDecryptsEveryEntryByDefault()
    {
        DecompilerResult result = await RunAsync(
            WriteArchive(encryptPlainEntry: true),
            allowDecryption: true,
            skipEncrypted: false
        );

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(
            await File.ReadAllBytesAsync(AxFixture()),
            await File.ReadAllBytesAsync(OutputFile("plain.ax"))
        );
        Assert.True(File.Exists(OutputFile("secret.hsp")));
    }

    [Fact]
    public async Task ArchiveEntryDecodesFromTheArchiveNotAnExistingFileOfItsName()
    {
        Directory.CreateDirectory(OutputDirectory);
        await File.WriteAllBytesAsync(OutputFile("secret.ax"), "not an AX image"u8.ToArray());

        DecompilerResult result = await RunAsync(
            WriteArchive(encryptPlainEntry: false),
            allowDecryption: true,
            skipEncrypted: false
        );

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(File.Exists(OutputFile("secret.hsp")));
        Assert.Equal(
            "not an AX image"u8.ToArray(),
            await File.ReadAllBytesAsync(OutputFile("secret.ax"))
        );
    }

    [Fact]
    public async Task DecompressDpmCountsEncryptedEntries()
    {
        var pipeline = new DecompilerPipeline(new NullLogger(), new NullProgressReporter());
        using var reader = new BinaryReader(File.OpenRead(WriteArchive(encryptPlainEntry: false)));

        DpmExtractionResult result = await pipeline.DecompressDpmAsync(
            reader,
            OutputDirectory,
            new DecompilerOptions { InputPath = "archive.dpm", SkipEncrypted = true }
        );

        Assert.Equal(2, result.Files.Count);
        Assert.Equal(1, result.EncryptedCount);
    }

    private string OutputDirectory => Path.Combine(_workDir, "out");

    private string OutputFile(string name) => Path.Combine(OutputDirectory, name);

    private static string AxFixture() =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ax", "doubleelse.ax");

    private Task<DecompilerResult> RunAsync(
        string archivePath,
        bool allowDecryption,
        bool skipEncrypted
    )
    {
        var pipeline = new DecompilerPipeline(new NullLogger(), new NullProgressReporter());
        Assert.True(pipeline.Initialize());
        var options = new DecompilerOptions
        {
            InputPath = archivePath,
            OutputDirectory = OutputDirectory,
            AllowDecryption = allowDecryption,
            SkipEncrypted = skipEncrypted,
        };
        return pipeline.RunAsync(options, CancellationToken.None);
    }

    // Writes a DPMX archive holding the same AX twice: "plain.ax" and "secret.ax", the second
    // always encrypted with a DPMX key and the first only when asked.
    //
    // 同じ AX を "plain.ax" と "secret.ax" の 2 つとして持つ DPMX アーカイブを書き出す。
    // 後者は常に DPMX の鍵で暗号化し、前者は指定された場合だけ暗号化する。
    private string WriteArchive(bool encryptPlainEntry)
    {
        byte[] ax = File.ReadAllBytes(AxFixture());
        byte[] encrypted = new HspCryptoTransform
        {
            XorAdd = new XorAddTransform
            {
                _xorByte = 0x5A,
                _addByte = 0x21,
                _xorSum = false,
            },
        }.Encryption(ax);

        var entries = new List<(string Name, int Key, byte[] Data)>
        {
            ("plain.ax", encryptPlainEntry ? 0x1234 : 0, encryptPlainEntry ? encrypted : ax),
            ("secret.ax", 0x1234, encrypted),
        };

        string path = Path.Combine(_workDir, "archive.dpm");
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write("DPMX"u8.ToArray());
        writer.Write(0);
        writer.Write(entries.Count);
        writer.Write(0);
        int offset = 0;
        foreach ((string name, int key, byte[] data) in entries)
        {
            byte[] nameField = new byte[16];
            System.Text.Encoding.ASCII.GetBytes(name).CopyTo(nameField, 0);
            writer.Write(nameField);
            writer.Write(-1);
            writer.Write(key);
            writer.Write(offset);
            writer.Write(data.Length);
            offset += data.Length;
        }

        foreach ((_, _, byte[] data) in entries)
        {
            writer.Write(data);
        }

        return path;
    }
}
