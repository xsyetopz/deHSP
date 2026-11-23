using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Pipeline;
using Xunit;

namespace HspDecompiler.Core.Tests.IntegrationTests;

// Each fixture is a debug build (hspcmp -d) of its .source.hsp files. Recompiling the expected
// files in debug mode gives back the same AX byte for byte, except dbghsp64: the 64-bit hspcmp
// sets hsp64 and its runtime without a directive, the source has no free line for the two
// directives a 32-bit hspcmp needs, so they add two line records at the end.
//
// 各フィクスチャは .source.hsp をデバッグビルド(hspcmp -d)したもの。期待ファイルを
// デバッグモードで再コンパイルするとバイト単位で同じ AX に戻る。例外は dbghsp64 である。64 ビット
// 版 hspcmp は指定無しで hsp64 とそのランタイムを設定し、32 ビット版が必要とする 2 つの指定を
// 入れる空き行がソースに無いため、末尾に 2 行分の記録が増える。
public class DecompileDebugAxTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ax", name);

    [Theory]
    [InlineData("dbgmultiline", "sub")]
    [InlineData("dbghsp64")]
    [InlineData("dbgmodules")]
    [InlineData("dbgmodinclude", "sub")]
    [InlineData("dbgmodinner", "inner")]
    [InlineData("dbgcolon")]
    public async Task DebugBuildDecompilesToItsSourceLayout(
        string fixture,
        params string[] includes
    )
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
            foreach (string include in includes)
            {
                await AssertSameBytes(
                    $"{fixture}.{include}.expected.hsp",
                    Path.Combine(root, include + ".hsp")
                );
            }
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
