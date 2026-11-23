using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Pipeline;
using Xunit;

namespace HspDecompiler.Core.Tests.IntegrationTests;

// Each fixture is a normal build (no -d) of its .source.hsp files by hspcmp 3.32, or 3.21 for
// nrmmulti; those releases record the lines of #module, #global and, from 3.3, {"..."} strings.
// Recompiling the expected files with the same hspcmp gives back the same AX byte for byte.
// nrmrestart's records restart in its include, so its output splits there.
//
// 各フィクスチャは .source.hsp を hspcmp 3.32(nrmmulti は 3.21)で通常ビルド(-d 無し)
// したもの。これらのリリースは #module・#global と、3.3 からは {"..."} 文字列の行を記録する。
// 期待ファイルを同じ hspcmp で再コンパイルするとバイト単位で同じ AX に戻る。nrmrestart の記録は
// 取り込みファイルで数え直すため、出力はそこで分かれる。
public class DecompileNormalAxTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ax", name);

    [Theory]
    [InlineData("nrmrestart", 1)]
    [InlineData("nrmdropmix", 0)]
    [InlineData("nrmstrings", 0)]
    [InlineData("nrmmulti", 0)]
    [InlineData("nrmmodinit", 0)]
    [InlineData("nrmstr2", 0)]
    public async Task NormalBuildDecompilesToItsRecordedLines(string fixture, int parts)
    {
        string root = Path.Combine(Path.GetTempPath(), $"hsp-decompiler-test-{Guid.NewGuid():N}");
        var pipeline = new DecompilerPipeline(new NullLogger(), new NullProgressReporter());
        Assert.True(pipeline.Initialize());
        var options = new DecompilerOptions
        {
            InputPath = Fixture($"{fixture}.ax"),
            OutputDirectory = root,
        };

        try
        {
            DecompilerResult result = await pipeline.RunAsync(options, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            await AssertSameBytes($"{fixture}.expected.hsp", result.OutputPath!);
            for (int part = 1; part <= parts; part++)
            {
                await AssertSameBytes(
                    $"{fixture}.{part}.expected.hsp",
                    Path.Combine(root, $"{fixture}_{part}.hsp")
                );
            }

            Assert.False(File.Exists(Path.Combine(root, $"{fixture}_{parts + 1}.hsp")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static async Task AssertSameBytes(string expected, string actual) =>
        Assert.Equal(
            await File.ReadAllBytesAsync(Fixture(expected)).ConfigureAwait(false),
            await File.ReadAllBytesAsync(actual).ConfigureAwait(false)
        );
}
