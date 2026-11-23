using System.CommandLine;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Pipeline;

namespace HspDecompiler.Cli;

// The dehsp root command. It writes through the invocation's Output and Error writers instead of
// Console, so tests can run the command in-process and read what it printed.
//
// dehsp のルートコマンド。Console ではなく呼び出し構成の Output と Error に書き込むため、
// テストはプロセス内でコマンドを実行して出力を読める。
internal sealed class DecompileCommand : RootCommand
{
    private readonly Argument<FileInfo> _input = new("input-file")
    {
        Description = "Input file to decompile (.ax, .exe, .dpm)",
    };

    private readonly Option<DirectoryInfo> _output = new("--output", "-o")
    {
        Description = "Output directory",
    };

    private readonly Option<FileInfo> _dictionary = new("--dictionary", "-d")
    {
        Description = "Custom dictionary XML (default: built-in HSP 3.8beta2 table)",
    };

    private readonly Option<bool> _noDecrypt = new("--no-decrypt")
    {
        Description = "Skip encrypted file decryption",
    };

    private readonly Option<bool> _skipEncrypted = new("--skip-encrypted")
    {
        Description = "Extract only non-encrypted files",
    };

    private readonly Option<bool> _utf8 = new("--utf8")
    {
        Description =
            "Write UTF-8 without a BOM instead of Shift_JIS; recompile with hspcmp -i or -j",
    };

    private readonly Option<bool> _verbose = new("--verbose", "-v")
    {
        Description = "Verbose logging",
    };

    internal DecompileCommand()
        : base("deHSP - decompile HSP 2 and HSP 3 programs")
    {
        Arguments.Add(_input);
        Options.Add(_output);
        Options.Add(_dictionary);
        Options.Add(_noDecrypt);
        Options.Add(_skipEncrypted);
        Options.Add(_utf8);
        Options.Add(_verbose);
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parseResult, CancellationToken ct)
    {
        TextWriter output = parseResult.InvocationConfiguration.Output;
        TextWriter error = parseResult.InvocationConfiguration.Error;
        var logger = new CliLogger(parseResult.GetValue(_verbose), output, error);
        var pipeline = new DecompilerPipeline(logger, new CliProgressReporter());

        string? dictionaryPath = parseResult.GetValue(_dictionary)?.FullName;
        bool loaded =
            dictionaryPath == null ? pipeline.Initialize() : pipeline.Initialize(dictionaryPath);
        if (!loaded)
        {
            await error
                .WriteLineAsync("Failed to load dictionary: " + dictionaryPath)
                .ConfigureAwait(false);
            return 1;
        }

        var options = new DecompilerOptions
        {
            InputPath = parseResult.GetRequiredValue(_input).FullName,
            OutputDirectory = parseResult.GetValue(_output)?.FullName,
            AllowDecryption = !parseResult.GetValue(_noDecrypt),
            SkipEncrypted = parseResult.GetValue(_skipEncrypted),
            Utf8Output = parseResult.GetValue(_utf8),
        };

        DecompilerResult result = await pipeline.RunAsync(options, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            await error.WriteLineAsync("Error: " + result.ErrorMessage).ConfigureAwait(false);
            return 1;
        }

        if (result.Warnings.Count > 0)
        {
            await error
                .WriteLineAsync($"{result.Warnings.Count} warning(s) during decompilation.")
                .ConfigureAwait(false);
        }

        if (result.OutputPath != null)
        {
            await output.WriteLineAsync("Output: " + result.OutputPath).ConfigureAwait(false);
        }
        return 0;
    }
}
