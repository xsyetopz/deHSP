using System;
using System.CommandLine;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Pipeline;

namespace HspDecompiler.Cli
{
    internal static class Program
    {
        static async Task<int> Main(string[] args)
        {
            var inputArg = new Argument<FileInfo>("input-file", "Input file to decompile (.ax, .exe, .dpm)");
            var outputOpt = new Option<DirectoryInfo>(new[] { "-o", "--output" }, "Output directory");
            var dictOpt = new Option<FileInfo>(new[] { "-d", "--dictionary" }, "Dictionary.csv path");
            var noDecryptOpt = new Option<bool>("--no-decrypt", "Skip encrypted file decryption");
            var skipEncOpt = new Option<bool>("--skip-encrypted", "Extract only non-encrypted files");
            var verboseOpt = new Option<bool>(new[] { "-v", "--verbose" }, "Verbose logging");

            var rootCommand = new RootCommand("HSP Decompiler - Decompile HSP2/HSP3 compiled files")
            {
                inputArg, outputOpt, dictOpt, noDecryptOpt, skipEncOpt, verboseOpt
            };

            rootCommand.SetHandler(async (input, output, dict, noDecrypt, skipEnc, verbose) =>
            {
                var logger = new CliLogger(verbose);
                var progress = new CliProgressReporter();
                var pipeline = new DecompilerPipeline(logger, progress);

                string dictPath = dict?.FullName ?? Path.Combine(AppContext.BaseDirectory, "Dictionary.csv");
                if (!pipeline.Initialize(dictPath))
                {
                    Console.Error.WriteLine("Failed to load dictionary: " + dictPath);
                    Environment.ExitCode = 1;
                    return;
                }

                var options = new DecompilerOptions
                {
                    InputPath = input.FullName,
                    OutputDirectory = output?.FullName ?? Path.GetDirectoryName(input.FullName) ?? ".",
                    DictionaryPath = dictPath,
                    AllowDecryption = !noDecrypt,
                    SkipEncrypted = skipEnc,
                    Verbose = verbose
                };

                var result = await pipeline.RunAsync(options, CancellationToken.None);
                if (!result.Success)
                {
                    Console.Error.WriteLine("Error: " + result.ErrorMessage);
                    Environment.ExitCode = 1;
                }
                else
                {
                    if (result.Warnings.Count > 0)
                        Console.Error.WriteLine($"{result.Warnings.Count} warning(s) during decompilation.");
                    if (result.OutputPath != null)
                        Console.WriteLine("Output: " + result.OutputPath);
                }
            }, inputArg, outputOpt, dictOpt, noDecryptOpt, skipEncOpt, verboseOpt);

            return await rootCommand.InvokeAsync(args);
        }
    }
}
