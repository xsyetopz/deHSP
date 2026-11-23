using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Pipeline;
using Xunit;

namespace HspDecompiler.Core.Tests.IntegrationTests;

public class DecompileAxFileTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ax", name);

    [Theory]
    [InlineData("keywords38b2", ".hsp", false)]
    [InlineData("doubleelse", ".hsp", false)]
    [InlineData("h2labels", ".as", false)]
    [InlineData("optprm", ".hsp", false)]
    [InlineData("h2include", ".as", false)]
    [InlineData("bootopts", ".hsp", false)]
    [InlineData("uncalledfunc", ".hsp", false)]
    [InlineData("pooledstrings", ".hsp", false)]
    [InlineData("labels", ".hsp", false)]
    [InlineData("utf8strings", ".hsp", true)]
    [InlineData("hsp64strings", ".hsp", true)]
    [InlineData("oldfunc", ".hsp", false)]
    [InlineData("int64params", ".hsp", false)]
    public async Task AxFixtureDecompilesToExpectedSourceInMissingOutputDirectory(
        string fixture,
        string extension,
        bool utf8Output
    )
    {
        string outputDir = NewOutputDirectory();
        DecompilerPipeline pipeline = NewPipeline();
        DecompilerOptions options = NewOptions(fixture, outputDir);
        options.Utf8Output = utf8Output;

        try
        {
            DecompilerResult result = await pipeline.RunAsync(options, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(Path.Combine(outputDir, fixture + extension), result.OutputPath);
            Assert.Equal(
                await File.ReadAllBytesAsync(Fixture($"{fixture}.expected{extension}")),
                await File.ReadAllBytesAsync(result.OutputPath!)
            );
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(outputDir)!, true);
        }
    }

    [Fact]
    public async Task Hsp2IncludeIsWrittenAsSeparateFileBesideMainSource()
    {
        string outputDir = NewOutputDirectory();
        DecompilerPipeline pipeline = NewPipeline();
        DecompilerOptions options = NewOptions("h2include", outputDir);

        try
        {
            DecompilerResult result = await pipeline.RunAsync(options, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(
                await File.ReadAllBytesAsync(Fixture("h2include.h2sub.expected.as")),
                await File.ReadAllBytesAsync(Path.Combine(outputDir, "h2sub.as"))
            );
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(outputDir)!, true);
        }
    }

    private static string NewOutputDirectory() =>
        Path.Combine(Path.GetTempPath(), $"hsp-decompiler-test-{Guid.NewGuid():N}", "nested");

    private static DecompilerPipeline NewPipeline()
    {
        var pipeline = new DecompilerPipeline(new NullLogger(), new NullProgressReporter());
        Assert.True(pipeline.Initialize());
        return pipeline;
    }

    private static DecompilerOptions NewOptions(string fixture, string outputDir) =>
        new() { InputPath = Fixture($"{fixture}.ax"), OutputDirectory = outputDir };
}
