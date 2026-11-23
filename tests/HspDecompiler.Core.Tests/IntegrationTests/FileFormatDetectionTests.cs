using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Pipeline;
using HspDecompiler.Core.Resources;
using Xunit;

namespace HspDecompiler.Core.Tests.IntegrationTests;

public class FileFormatDetectionTests
{
    [Fact]
    public async Task InputWithoutKnownMagicFailsAsUnrecognized()
    {
        // Arrange
        string dir = NewDirectory();
        string input = Path.Combine(dir, "junk.ax");
        await File.WriteAllBytesAsync(input, "JUNKJUNKJUNKJUNK"u8.ToArray());
        DecompilerPipeline pipeline = NewPipeline();

        try
        {
            // Act
            DecompilerResult result = await pipeline.RunAsync(
                new DecompilerOptions { InputPath = input, OutputDirectory = dir },
                CancellationToken.None
            );

            // Assert
            Assert.False(result.Success);
            Assert.Equal(Strings.UnrecognizedFileFormat, result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task AxIsRecognizedByMagicRatherThanExtension()
    {
        // Arrange
        string dir = NewDirectory();
        string input = Path.Combine(dir, "renamed.bin");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ax", "doubleelse.ax"), input);
        DecompilerPipeline pipeline = NewPipeline();

        try
        {
            // Act
            DecompilerResult result = await pipeline.RunAsync(
                new DecompilerOptions { InputPath = input, OutputDirectory = dir },
                CancellationToken.None
            );

            // Assert
            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(Path.Combine(dir, "renamed.hsp"), result.OutputPath);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string NewDirectory() =>
        Directory
            .CreateDirectory(
                Path.Combine(Path.GetTempPath(), $"hsp-detect-test-{Guid.NewGuid():N}")
            )
            .FullName;

    private static DecompilerPipeline NewPipeline()
    {
        var pipeline = new DecompilerPipeline(new NullLogger(), new NullProgressReporter());
        Assert.True(pipeline.Initialize());
        return pipeline;
    }
}
