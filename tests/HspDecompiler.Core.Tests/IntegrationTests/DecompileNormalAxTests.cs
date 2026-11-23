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
// nrmrestart's records restart in its include, so its output splits there. nrmmacro's record
// comes from a "%c" macro, which the output writes again as __line_record.
//
// 各フィクスチャは .source.hsp を hspcmp 3.32(nrmmulti は 3.21)で通常ビルド(-d 無し)
// したもの。これらのリリースは #module・#global と、3.3 からは {"..."} 文字列の行を記録する。
// 期待ファイルを同じ hspcmp で再コンパイルするとバイト単位で同じ AX に戻る。nrmrestart の記録は
// 取り込みファイルで数え直すため、出力はそこで分かれる。nrmmacro の記録は "%c" マクロから
// 来ており、出力はそれを __line_record として書き直す。
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
    [InlineData("nrmmacro", 0)]
    public Task NormalBuildDecompilesToItsRecordedLines(string fixture, int parts) =>
        AssertDecompiles(fixture, parts);

    // nrmnofit's "%c" macro runs on line 1 of its include, so its record names line 2 of a file
    // whose line 1 the macro use already takes. No event and no free record fits that, so the
    // lines come out in order. hspcmp 3.32 compiles that output to the same code and data
    // segments, without the record.
    //
    // nrmnofit の "%c" マクロは取り込みファイルの 1 行目で使われるため、記録はマクロの使用が
    // 1 行目を占めるファイルの 2 行目を示す。どの位置も自由な記録もそれには収まらないため、行は
    // 順に出力する。hspcmp 3.32 はその出力を、記録を除いて同じコードとデータの区画に
    // コンパイルする。
    [Fact]
    public Task NormalBuildWhoseRecordsFitNoLayoutDecompilesInOrder() =>
        AssertDecompiles("nrmnofit", 0);

    private static async Task AssertDecompiles(string fixture, int parts)
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
            DecompilerResult result = await pipeline
                .RunAsync(options, CancellationToken.None)
                .ConfigureAwait(false);

            Assert.True(result.Success, result.ErrorMessage);
            await AssertSameBytes($"{fixture}.expected.hsp", result.OutputPath!)
                .ConfigureAwait(false);
            for (int part = 1; part <= parts; part++)
            {
                await AssertSameBytes(
                        $"{fixture}.{part}.expected.hsp",
                        Path.Combine(root, $"{fixture}_{part}.hsp")
                    )
                    .ConfigureAwait(false);
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
