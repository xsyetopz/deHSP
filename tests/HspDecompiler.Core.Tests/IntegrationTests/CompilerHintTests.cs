using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Pipeline;
using Xunit;

namespace HspDecompiler.Core.Tests.IntegrationTests;

// styles.ax comes from hspcmp 3.7, whose header word 0x360 it shares with 3.6 and 3.8β2, and
// h2labels.ax carries the HSP 2 word 0x206 that 2.6 and 2.61 share. A hint among those is taken
// silently; a hint from another word is refused with a warning, and the output stays the one
// inference alone gives.
//
// styles.ax は hspcmp 3.7 製で、ヘッダーのワード 0x360 を 3.6・3.8β2 と共有する。h2labels.ax は
// 2.6 と 2.61 が共有する HSP 2 のワード 0x206 を持つ。その中の指定は黙って採用し、別のワードの
// 指定は警告して退け、出力は推定だけの時と同じになる。
public sealed class CompilerHintTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"hsp-decompiler-test-{Guid.NewGuid():N}"
    );

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Theory]
    [InlineData("styles", ".hsp", "3.7", false)]
    [InlineData("styles", ".hsp", "3.1", true)]
    [InlineData("h2labels", ".as", "2.61", false)]
    [InlineData("h2labels", ".as", "3.7", true)]
    public async Task HintIsTakenOnlyWhenTheAxAllowsIt(
        string fixture,
        string extension,
        string hint,
        bool warned
    )
    {
        var logger = new NullLogger();
        var pipeline = new DecompilerPipeline(logger, new NullProgressReporter());
        Assert.True(pipeline.Initialize());
        var options = new DecompilerOptions
        {
            InputPath = Fixture(fixture + ".ax"),
            OutputDirectory = _root,
            CompilerRelease = hint,
        };

        DecompilerResult result = await pipeline.RunAsync(options, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(warned, logger.Warnings.Any(w => w.Contains(hint, StringComparison.Ordinal)));
        Assert.Equal(
            await File.ReadAllBytesAsync(Fixture(fixture + ".expected" + extension)),
            await File.ReadAllBytesAsync(result.OutputPath!)
        );
    }

    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ax", name);
}
