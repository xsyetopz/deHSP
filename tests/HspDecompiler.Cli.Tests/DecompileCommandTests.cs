using System;
using System.CommandLine;
using System.IO;
using System.Text;
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

    [Fact]
    public async Task Utf8OptionWritesUtf8WithoutByteOrderMark()
    {
        int exitCode = await InvokeAsync(Fixture("utf8strings.ax"), "--output", _workDir, "--utf8");

        Assert.Equal(0, exitCode);
        byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(_workDir, "utf8strings.hsp"));
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..]);
        Assert.Contains(
            "\"café ✓ 日本\"",
            Encoding.UTF8.GetString(bytes),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task StyleOptionsChangeTheSpelling()
    {
        int exitCode = await InvokeAsync(
            Fixture("styles.ax"),
            "--output",
            _workDir,
            "--if-parens",
            "tight",
            "--logical",
            "doubled",
            "--compact",
            "--paren-negatives",
            "--hex",
            "all",
            "--no-macros"
        );

        Assert.Equal(0, exitCode);
        string source = await File.ReadAllTextAsync(Path.Combine(_workDir, "styles.hsp"));
        Assert.Contains("if ((var_0==1)&&(var_1==2)) {", source, StringComparison.Ordinal);
        Assert.Contains("var_3 = var_0-(-1)+var_1*(-2)", source, StringComparison.Ordinal);
        Assert.Contains("mes dirinfo(1)+dirinfo(0)+dirinfo(5)", source, StringComparison.Ordinal);
        Assert.Contains("poke var_5, 0, 0xEF", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompilerHintTheAxContradictsWarnsAndStillDecompiles()
    {
        int exitCode = await InvokeAsync(
            Fixture("styles.ax"),
            "--output",
            _workDir,
            "--compiler",
            "3.1"
        );

        Assert.Equal(0, exitCode);
        Assert.Contains("warning(s)", _error.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_workDir, "styles.hsp")));
    }

    private static string Fixture(string name = "doubleelse.ax") =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private Task<int> InvokeAsync(params string[] args) =>
        new DecompileCommand()
            .Parse(args)
            .InvokeAsync(new InvocationConfiguration { Output = _output, Error = _error });
}
