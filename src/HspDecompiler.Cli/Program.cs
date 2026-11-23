using System;
using System.CommandLine;
using System.IO;
using HspDecompiler.Cli;
using HspDecompiler.Core.Pipeline;

string[] OutputAliases = ["-o"];
string[] DictAliases = ["-d"];
string[] VerboseAliases = ["-v"];

var inputArg = new Argument<FileInfo>("input-file")
{
    Description = "Input file to decompile (.ax, .exe, .dpm)",
};
var outputOpt = new Option<DirectoryInfo>("--output", OutputAliases)
{
    Description = "Output directory",
};
var dictOpt = new Option<FileInfo>("--dictionary", DictAliases)
{
    Description = "Custom dictionary XML (default: built-in HSP 3.8beta2 table)",
};
var noDecryptOpt = new Option<bool>("--no-decrypt")
{
    Description = "Skip encrypted file decryption",
};
var skipEncOpt = new Option<bool>("--skip-encrypted")
{
    Description = "Extract only non-encrypted files",
};
var verboseOpt = new Option<bool>("--verbose", VerboseAliases) { Description = "Verbose logging" };

var rootCommand = new RootCommand("deHSP - decompile HSP 2 and HSP 3 programs")
{
    inputArg,
    outputOpt,
    dictOpt,
    noDecryptOpt,
    skipEncOpt,
    verboseOpt,
};

rootCommand.SetAction(
    async (parseResult, ct) =>
    {
        FileInfo input = parseResult.GetValue(inputArg)!;
        DirectoryInfo? output = parseResult.GetValue(outputOpt);
        FileInfo? dict = parseResult.GetValue(dictOpt);
        bool noDecrypt = parseResult.GetValue(noDecryptOpt);
        bool skipEnc = parseResult.GetValue(skipEncOpt);
        bool verbose = parseResult.GetValue(verboseOpt);

        var logger = new CliLogger(verbose);
        var progress = new CliProgressReporter();
        var pipeline = new DecompilerPipeline(logger, progress);

        string? dictPath = dict?.FullName;
        if (!(dictPath == null ? pipeline.Initialize() : pipeline.Initialize(dictPath)))
        {
            Console.Error.WriteLine("Failed to load dictionary: " + dictPath);
            return 1;
        }

        var options = new DecompilerOptions
        {
            InputPath = input.FullName,
            OutputDirectory = output?.FullName,
            AllowDecryption = !noDecrypt,
            SkipEncrypted = skipEnc,
        };

        DecompilerResult result = await pipeline.RunAsync(options, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            Console.Error.WriteLine("Error: " + result.ErrorMessage);
            return 1;
        }

        if (result.Warnings.Count > 0)
        {
            Console.Error.WriteLine($"{result.Warnings.Count} warning(s) during decompilation.");
        }

        if (result.OutputPath != null)
        {
            Console.WriteLine("Output: " + result.OutputPath);
        }
        return 0;
    }
);

return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
