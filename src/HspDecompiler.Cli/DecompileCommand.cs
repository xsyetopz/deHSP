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
    private static readonly SourceStyle s_defaults = new();

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
        Description =
            "Custom dictionary XML (default: built-in table for ASC 1.1 to hspcmp 3.8beta2)",
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

    private readonly Option<DirectoryInfo> _common = new("--common")
    {
        Description =
            "HSP common directory; restores the #include of a matching common header in a debug build",
    };

    private readonly Option<string> _compiler = new("--compiler")
    {
        Description =
            "hspcmp release that wrote the program, such as 3.32 or 3.8b2; a hint the AX contradicts is ignored with a warning",
    };

    private readonly Option<IfParentheses> _ifParens = new("--if-parens")
    {
        Description = "Parentheses around an if condition: none, tight '(x)', spaced '( x )'",
        DefaultValueFactory = _ => s_defaults.IfParentheses,
    };

    private readonly Option<OneLineIf> _oneLineIf = new("--one-line-if")
    {
        Description = "An if block on one source line (debug builds): colon 'if x : y', braces",
        DefaultValueFactory = _ => s_defaults.OneLineIf,
    };

    private readonly Option<bool> _parenNegatives = new("--paren-negatives")
    {
        Description = "Write a negative literal beside an operator as 'a - (-1)'",
    };

    private readonly Option<LogicalOperators> _logical = new("--logical")
    {
        Description = "& and | between conditions: symbols, doubled '&&', words 'and'",
        DefaultValueFactory = _ => s_defaults.LogicalOperators,
    };

    private readonly Option<bool> _compact = new("--compact")
    {
        Description = "Write binary operators without spaces: 'a+1'",
    };

    private readonly Option<bool> _noMacros = new("--no-macros")
    {
        Description = "Write hspdef.as macro calls expanded: 'dirinfo(1)' for 'dir_exe'",
    };

    private readonly Option<HexLiterals> _hex = new("--hex")
    {
        Description = "Integers in hexadecimal: none, poke (poke values of 128 or more), all",
        DefaultValueFactory = _ => s_defaults.HexLiterals,
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
        Options.Add(_common);
        Options.Add(_compiler);
        Options.Add(_ifParens);
        Options.Add(_oneLineIf);
        Options.Add(_parenNegatives);
        Options.Add(_logical);
        Options.Add(_compact);
        Options.Add(_noMacros);
        Options.Add(_hex);
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
            CommonDirectory = parseResult.GetValue(_common)?.FullName,
            CompilerRelease = parseResult.GetValue(_compiler),
            Style = Style(parseResult),
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

    private SourceStyle Style(ParseResult parseResult) =>
        new()
        {
            IfParentheses = parseResult.GetValue(_ifParens),
            OneLineIf = parseResult.GetValue(_oneLineIf),
            ParenthesizeNegatives = parseResult.GetValue(_parenNegatives),
            LogicalOperators = parseResult.GetValue(_logical),
            CompactOperators = parseResult.GetValue(_compact),
            StandardMacros = !parseResult.GetValue(_noMacros),
            HexLiterals = parseResult.GetValue(_hex),
        };
}
