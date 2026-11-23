using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace HspDecompiler.Cli.Tests;

public sealed class DecompileCommandTests : IDisposable
{
    private readonly string _workDir = Path.Combine(
        Path.GetTempPath(),
        $"dehsp-cli-test-{Guid.NewGuid():N}"
    );
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    public DecompileCommandTests()
    {
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose()
    {
        Directory.Delete(_workDir, true);
        _output.Dispose();
        _error.Dispose();
    }

    [Fact]
    public async Task HelpListsTheOptionsAndSucceeds()
    {
        int exitCode = await InvokeAsync("--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("--skip-encrypted", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingInputArgumentIsAParseError()
    {
        int exitCode = await InvokeAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains("input-file", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonexistentInputFileFails()
    {
        int exitCode = await InvokeAsync(Path.Combine(_workDir, "missing.ax"));

        Assert.Equal(1, exitCode);
        Assert.Contains("Error:", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnreadableDictionaryFails()
    {
        string dictionary = Path.Combine(_workDir, "missing.xml");

        int exitCode = await InvokeAsync(Fixture(), "--dictionary", dictionary);

        Assert.Equal(1, exitCode);
        Assert.Contains("Failed to load dictionary", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecompilesAnAxFileIntoTheOutputDirectory()
    {
        int exitCode = await InvokeAsync(Fixture(), "--output", _workDir);

        Assert.Equal(0, exitCode);
        Assert.Contains("Output: ", _output.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_workDir, "doubleelse.hsp")));
    }

    private static string Fixture() =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "doubleelse.ax");

    private Task<int> InvokeAsync(params string[] args) =>
        new DecompileCommand()
            .Parse(args)
            .InvokeAsync(new InvocationConfiguration { Output = _output, Error = _error });
}
