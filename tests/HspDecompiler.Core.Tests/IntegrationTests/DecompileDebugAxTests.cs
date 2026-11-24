using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Pipeline;
using Xunit;

namespace HspDecompiler.Core.Tests.IntegrationTests;

// Each fixture is a debug build (hspcmp -d) of its .source.hsp files. Recompiling the expected
// files in debug mode with the compiler that wrote the fixture gives back the same AX byte for
// byte. dbghsp64 comes from the 64-bit hspcmp.exe of 3.8β2; the 32-bit hspcmp.dll of the same
// release writes 32-bit parameter frames, so no source rebuilds that AX with it. dbghsp64plain
// has no user function and no code-free line, so its runtime alone names the 64-bit compiler.
//
// 各フィクスチャは .source.hsp をデバッグビルド(hspcmp -d)したもの。期待ファイルを、その
// フィクスチャを書いたコンパイラでデバッグモードで再コンパイルするとバイト単位で同じ AX に戻る。
// dbghsp64 は 3.8β2 の 64 ビット版 hspcmp.exe のもので、同じリリースの 32 ビット版 hspcmp.dll は
// 32 ビットの引数フレームを書くため、どのソースからもその AX は作れない。dbghsp64plain は
// ユーザー関数もコードの無い行も持たないため、ランタイムだけで 64 ビット版のコンパイラが決まる。
public class DecompileDebugAxTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ax", name);

    [Theory]
    [InlineData("dbgmultiline", "sub")]
    [InlineData("dbghsp64")]
    [InlineData("dbghsp64plain")]
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
