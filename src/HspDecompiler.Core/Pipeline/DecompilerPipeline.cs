using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax2;
using HspDecompiler.Core.Ax2.Data;
using HspDecompiler.Core.Ax3;
using HspDecompiler.Core.DpmToAx;
using HspDecompiler.Core.DpmToAx.Crypto;
using HspDecompiler.Core.Encoding;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Pipeline;

public sealed class DecompilerPipeline
{
    private const string Hsp2Magic = "HSP2";
    private const string Hsp3Magic = "HSP3";

    private static readonly CompositeFormat s_dictionaryLoadSuccessFormat = CompositeFormat.Parse(
        Strings.DictionaryLoadSuccess
    );

    private static readonly CompositeFormat s_dictionaryLoadFailedFormat = CompositeFormat.Parse(
        Strings.DictionaryLoadFailed
    );

    private static readonly CompositeFormat s_readingFileFormat = CompositeFormat.Parse(
        Strings.ReadingFile
    );

    private static readonly CompositeFormat s_directoryCreateFailedFormat = CompositeFormat.Parse(
        Strings.DirectoryCreateFailed
    );

    private static readonly CompositeFormat s_fileEncryptedFormat = CompositeFormat.Parse(
        Strings.FileEncrypted
    );

    private static readonly CompositeFormat s_fileAlreadyExistsFormat = CompositeFormat.Parse(
        Strings.FileAlreadyExists
    );

    private static readonly CompositeFormat s_fileSeekFailedFormat = CompositeFormat.Parse(
        Strings.FileSeekFailed
    );

    private static readonly CompositeFormat s_fileSaveFailedFormat = CompositeFormat.Parse(
        Strings.FileSaveFailed
    );

    private static readonly CompositeFormat s_decryptingFileFormat = CompositeFormat.Parse(
        Strings.DecryptingFile
    );

    private static readonly CompositeFormat s_decryptionFailedFormat = CompositeFormat.Parse(
        Strings.DecryptionFailed
    );

    private static readonly CompositeFormat s_outputtingToFormat = CompositeFormat.Parse(
        Strings.OutputtingTo
    );

    private readonly IDecompilerLogger _logger;
    private readonly IProgressReporter _progress;
    private Hsp3Dictionary? _dictionary;

    public DecompilerPipeline(IDecompilerLogger logger, IProgressReporter progress)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(progress);
        _logger = logger;
        _progress = progress;
    }

    /// <summary>
    /// Loads the HSP keyword dictionary built into the library.
    /// </summary>
    /// <returns><see langword="true"/> once the dictionary is ready.</returns>
    public bool Initialize()
    {
        _dictionary = Hsp3Dictionary.BuiltIn;
        _logger.Write(
            string.Format(
                CultureInfo.InvariantCulture,
                s_dictionaryLoadSuccessFormat,
                "Dictionary.json " + _dictionary.Version
            )
        );
        return true;
    }

    /// <summary>
    /// Loads an HSP keyword dictionary from a JSON file in the format of the built-in one.
    /// </summary>
    /// <param name="dictionaryPath">Path of the dictionary JSON file.</param>
    /// <returns>
    /// <see langword="true"/> when the file loaded; <see langword="false"/> when it is missing
    /// or malformed, after logging the failure.
    /// </returns>
    public bool Initialize(string dictionaryPath)
    {
        _dictionary = Hsp3Dictionary.FromFile(dictionaryPath);
        _logger.Write(
            string.Format(
                CultureInfo.InvariantCulture,
                _dictionary != null ? s_dictionaryLoadSuccessFormat : s_dictionaryLoadFailedFormat,
                Path.GetFileName(dictionaryPath)
            )
        );
        return _dictionary != null;
    }

    public async Task<DecompilerResult> RunAsync(
        DecompilerOptions options,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        var decompilerOutput = new DecompilerResult();

        try
        {
            _logger.Write(
                string.Format(
                    CultureInfo.InvariantCulture,
                    s_readingFileFormat,
                    Path.GetFileName(options.InputPath)
                )
            );
            var stream = new FileStream(options.InputPath, FileMode.Open, FileAccess.Read);
            await using ConfiguredAsyncDisposable streamScope = stream.ConfigureAwait(false);
            using var reader = new BinaryReader(stream, ShiftJisHelper.Encoding);

            string? errorLogPath = await DecompileInputAsync(reader, options, decompilerOutput, ct)
                .ConfigureAwait(false);
            if (errorLogPath == null)
            {
                return decompilerOutput;
            }

            WriteWarningLog(errorLogPath, decompilerOutput);
            decompilerOutput.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
            decompilerOutput.Success = false;
            decompilerOutput.ErrorMessage = ex.Message;
        }

        return decompilerOutput;
    }

    // Returns the warning log path, or null when an archive leaves nothing to decompile.
    // ---
    // 警告ログのパスを返す。アーカイブにデコンパイル対象が残らない場合は null を返す。
    private async Task<string?> DecompileInputAsync(
        BinaryReader reader,
        DecompilerOptions options,
        DecompilerResult decompilerOutput,
        CancellationToken ct
    )
    {
        string magic = DetectFileFormat(reader);
        string? explicitDir = string.IsNullOrEmpty(options.OutputDirectory)
            ? null
            : Path.TrimEndingDirectorySeparator(options.OutputDirectory)
                + Path.DirectorySeparatorChar;

        if (
            magic.StartsWith(Hsp2Magic, StringComparison.Ordinal)
            || magic.StartsWith(Hsp3Magic, StringComparison.Ordinal)
        )
        {
            return await DecompileAxAsync(reader, magic, explicitDir, options, decompilerOutput, ct)
                .ConfigureAwait(false);
        }

        if (
            !magic.StartsWith("MZ", StringComparison.Ordinal)
            && !magic.StartsWith("DPM", StringComparison.Ordinal)
        )
        {
            throw new HspDecoderException(Strings.UnrecognizedFileFormat);
        }

        string outputDir = explicitDir
            ?? BuildAutoIncrementDirName(InputDirectory(options), InputBaseName(options));
        bool decompiled = await DecompileArchiveAsync(
                reader,
                outputDir,
                options,
                decompilerOutput,
                ct
            )
            .ConfigureAwait(false);
        return decompiled ? outputDir.TrimEnd(Path.DirectorySeparatorChar) + ".log" : null;
    }

    // Returns the warning log path for the decompiled source.
    // ---
    // デコンパイルしたソースの警告ログのパスを返す。
    private async Task<string> DecompileAxAsync(
        BinaryReader reader,
        string magic,
        string? explicitDir,
        DecompilerOptions options,
        DecompilerResult decompilerOutput,
        CancellationToken ct
    )
    {
        if (explicitDir != null)
        {
            Directory.CreateDirectory(explicitDir);
        }

        string outputPath = BuildAutoIncrementFileName(
            explicitDir ?? InputDirectory(options),
            InputBaseName(options),
            SourceExtension(magic)
        );
        decompilerOutput.OutputPath = outputPath;
        await DecodeAsync(reader, outputPath, ct).ConfigureAwait(false);
        return outputPath + ".log";
    }

    private static string InputDirectory(DecompilerOptions options) =>
        (Path.GetDirectoryName(options.InputPath) ?? ".") + Path.DirectorySeparatorChar;

    private static string InputBaseName(DecompilerOptions options) =>
        Path.GetFileNameWithoutExtension(options.InputPath);

    private static string SourceExtension(string axMagic) =>
        axMagic.StartsWith(Hsp2Magic, StringComparison.Ordinal) ? ".as" : ".hsp";

    // Returns false when nothing past the extraction is left to decompile.
    // 展開後にデコンパイルするものが無い場合は false を返す。
    private async Task<bool> DecompileArchiveAsync(
        BinaryReader reader,
        string outputDir,
        DecompilerOptions options,
        DecompilerResult decompilerOutput,
        CancellationToken ct
    )
    {
        outputDir = Path.TrimEndingDirectorySeparator(outputDir) + Path.DirectorySeparatorChar;
        DpmExtractionResult dpmResult = await DecompressDpmAsync(reader, outputDir, options, ct)
            .ConfigureAwait(false);
        decompilerOutput.DpmFiles.AddRange(dpmResult.Files);
        decompilerOutput.OutputPath = outputDir;

        if (dpmResult.AllEncrypted || dpmResult.Cancelled)
        {
            decompilerOutput.Success = !dpmResult.AllEncrypted;
            if (dpmResult.AllEncrypted)
            {
                decompilerOutput.ErrorMessage = Strings.AllFilesEncrypted;
            }

            return false;
        }

        foreach (DpmFileEntry entry in dpmResult.Files)
        {
            string axPath = Path.Combine(outputDir, entry.FileName ?? "");
            if (
                entry.FileName == null
                || !entry.FileName.EndsWith(".ax", StringComparison.OrdinalIgnoreCase)
                || !File.Exists(axPath)
            )
            {
                continue;
            }

            var axStream = new FileStream(axPath, FileMode.Open, FileAccess.Read);
            await using ConfiguredAsyncDisposable axScope = axStream.ConfigureAwait(false);
            using var axReader = new BinaryReader(axStream, ShiftJisHelper.Encoding);
            string outputPath = BuildAutoIncrementFileName(
                outputDir,
                Path.GetFileNameWithoutExtension(entry.FileName),
                SourceExtension(DetectFileFormat(axReader))
            );
            await DecodeAsync(axReader, outputPath, ct).ConfigureAwait(false);
        }

        return true;
    }

    private void WriteWarningLog(string errorLogPath, DecompilerResult decompilerOutput)
    {
        if (_logger.Warnings.Count == 0)
        {
            return;
        }

        using (var errorLog = new StreamWriter(errorLogPath, false, ShiftJisHelper.Encoding))
        {
            foreach (string warning in _logger.Warnings)
            {
                errorLog.WriteLine(warning);
            }
        }

        decompilerOutput.Warnings.AddRange(_logger.Warnings);
    }

    private static string DetectFileFormat(BinaryReader reader)
    {
        char[] buffer = reader.ReadChars(4);
        string magic = new(buffer);
        reader.BaseStream.Seek(0, SeekOrigin.Begin);
        return magic;
    }

    public async Task<DpmExtractionResult> DecompressDpmAsync(
        BinaryReader reader,
        string outputDir,
        DecompilerOptions options,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(reader);

        var dpmResult = new DpmExtractionResult();

        _logger.Write(Strings.SearchingDpmHeader);
        DpmExtractor extractor = DpmExtractor.FromBinaryReader(reader)
            ?? throw new HspDecoderException(Strings.DpmHeaderNotFound);
        if (extractor.FileList == null || extractor.FileList.Count == 0)
        {
            throw new HspDecoderException(Strings.DpmNoFiles);
        }

        dpmResult.Files.AddRange(extractor.FileList);
        int encryptedCount = extractor.FileList.Count(file => file.IsEncrypted);
        dpmResult.EncryptedCount = encryptedCount;

        int totalCount = extractor.FileList.Count;
        if (totalCount == encryptedCount && !options.AllowDecryption)
        {
            dpmResult.AllEncrypted = true;
            _logger.Write(Strings.ExtractionCancelled);
            return dpmResult;
        }

        if (encryptedCount > 0 && options is { SkipEncrypted: false, AllowDecryption: false })
        {
            dpmResult.Cancelled = true;
            _logger.Write(Strings.ExtractionCancelled);
            return dpmResult;
        }

        CreateOutputDirectory(outputDir);

        Dpm2CryptoTransform? dpm2Crypto = null;
        if (extractor.IsDpm2 && extractor.CrcSeed != 0)
        {
            dpm2Crypto = Dpm2CryptoTransform.FromCrcSeed(extractor.CrcSeed, extractor.Salt);
        }

        foreach (DpmFileEntry file in extractor.FileList)
        {
            await WriteDpmEntryAsync(reader, extractor, file, outputDir, options, dpm2Crypto, ct)
                .ConfigureAwait(false);
        }

        _logger.Write(Strings.ExtractionComplete);
        return dpmResult;
    }

    private static void CreateOutputDirectory(string outputDir)
    {
        try
        {
            Directory.CreateDirectory(outputDir);
        }
        catch (Exception ex)
        {
            throw new HspDecoderException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    s_directoryCreateFailedFormat,
                    outputDir
                ),
                ex
            );
        }
    }

    private async Task WriteDpmEntryAsync(
        BinaryReader reader,
        DpmExtractor extractor,
        DpmFileEntry file,
        string outputDir,
        DecompilerOptions options,
        Dpm2CryptoTransform? dpm2Crypto,
        CancellationToken ct
    )
    {
        if (file.IsEncrypted && !options.AllowDecryption)
        {
            _logger.Write(
                string.Format(CultureInfo.InvariantCulture, s_fileEncryptedFormat, file.FileName)
            );
            return;
        }

        string outputPath = Path.Combine(outputDir, file.FileName ?? "");
        if (File.Exists(outputPath))
        {
            _logger.Write(
                string.Format(
                    CultureInfo.InvariantCulture,
                    s_fileAlreadyExistsFormat,
                    file.FileName
                )
            );
            return;
        }

        if (!extractor.Seek(file))
        {
            _logger.Write(
                string.Format(CultureInfo.InvariantCulture, s_fileSeekFailedFormat, file.FileName)
            );
            return;
        }

        byte[] fileData = reader.ReadBytes(file.FileSize);

        if (dpm2Crypto != null && file.Checksum != 0)
        {
            fileData = dpm2Crypto.Decrypt(fileData, file.Checksum);
        }

        if (file.IsEncrypted)
        {
            byte[]? decrypted = CrackDpmEncryption(fileData, file);
            if (decrypted == null)
            {
                return;
            }
            fileData = decrypted;
        }

        await SaveDpmEntryAsync(fileData, file, outputPath, ct).ConfigureAwait(false);
    }

    private async Task SaveDpmEntryAsync(
        byte[] fileData,
        DpmFileEntry file,
        string outputPath,
        CancellationToken ct
    )
    {
        try
        {
            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var saveStream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write);
            await using (saveStream.ConfigureAwait(false))
            {
                await saveStream.WriteAsync(fileData, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(
                string.Format(CultureInfo.InvariantCulture, s_fileSaveFailedFormat, file.FileName)
                    + " "
                    + ex.Message
            );
        }
    }

    private byte[]? CrackDpmEncryption(byte[] fileData, DpmFileEntry file)
    {
        _logger.Write(
            string.Format(CultureInfo.InvariantCulture, s_decryptingFileFormat, file.FileName)
        );

        var decryptor = HspCryptoTransform.CrackEncryption(fileData, Validator);
        if (decryptor != null)
        {
            return decryptor.Decryption(fileData);
        }

        _logger.Write(
            string.Format(CultureInfo.InvariantCulture, s_decryptionFailedFormat, file.FileName)
        );
        return null;

        // A key counts only when the whole AX decodes; the magic alone matches many wrong keys.
        // ---
        // マジックだけでは誤った鍵も多く一致するため、AX 全体を解析できた鍵だけを採用する。
        bool Validator(byte[] decryptedData)
        {
            try
            {
                using var ms = new MemoryStream(decryptedData);
                using var br = new BinaryReader(ms, ShiftJisHelper.Encoding);
                string fileMagic = DetectFileFormat(br);
                if (
                    !fileMagic.StartsWith(Hsp2Magic, StringComparison.Ordinal)
                    && !fileMagic.StartsWith(Hsp3Magic, StringComparison.Ordinal)
                )
                {
                    return false;
                }

                // CrackEncryption takes a synchronous validator, so the decode blocks here.
                // ---
                // CrackEncryption は同期の検証関数を受け取るため、ここでは解析を同期で待つ。
                CreateDecoder(br)
                    .DecodeAsync(
                        br,
                        NullDecompilerLogger.Instance,
                        _progress,
                        CancellationToken.None
                    )
                    .GetAwaiter()
                    .GetResult();
                return true;
            }
            catch (Exception ex) when (ex is HspDecoderException or IOException or FormatException)
            {
                return false;
            }
        }
    }

    public async Task DecodeAsync(
        BinaryReader reader,
        string outputPath,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (_dictionary == null)
        {
            throw new InvalidOperationException(Strings.DictionaryNotInitialized);
        }

        _logger.StartSection();
        _logger.Write(Strings.Decompiling);
        _logger.StartSection();

        IAxDecoder decoder = CreateDecoder(reader, Path.GetFileName(outputPath));
        List<string> lines = await decoder
            .DecodeAsync(reader, _logger, _progress, ct)
            .ConfigureAwait(false);

        _logger.EndSection();
        _logger.Write(Strings.DecompileComplete);
        _logger.EndSection();
        _logger.Write(
            string.Format(
                CultureInfo.InvariantCulture,
                s_outputtingToFormat,
                Path.GetFileName(outputPath)
            )
        );

        WriteOutputLines(lines, outputPath);
        if (decoder is Ax2Decoder ax2)
        {
            WriteIncludeFiles(ax2.Includes, Path.GetDirectoryName(outputPath) ?? ".");
        }

        _logger.Write(Strings.OutputComplete);
    }

    private IAxDecoder CreateDecoder(BinaryReader reader, string? mainFileName = null)
    {
        long startPosition = reader.BaseStream.Position;
        char[] buffer = reader.ReadChars(4);
        string magic = new(buffer);
        reader.BaseStream.Seek(startPosition, SeekOrigin.Begin);

        if (magic.Equals(Hsp2Magic, StringComparison.Ordinal))
        {
            return new Ax2Decoder { MainFileName = mainFileName };
        }

        if (magic.Equals(Hsp3Magic, StringComparison.Ordinal))
        {
            var decoder = new Ax3Decoder { Dictionary = _dictionary };
            return decoder;
        }

        throw new HspDecoderException(Strings.NotHsp2OrHsp3);
    }

    private static void WriteOutputLines(List<string> lines, string outputPath)
    {
        using var writer = new StreamWriter(outputPath, false, ShiftJisHelper.Encoding);
        foreach (string line in lines)
        {
            writer.WriteLine(line);
        }
    }

    // An include keeps the exact name hspcmp recorded, so an existing file of that name is left
    // alone rather than renamed or overwritten.
    // ---
    // 取り込みファイルは hspcmp が記録した名前のままにする必要があるため、同名のファイルが既に
    // あれば名前を変えたり上書きしたりせずに残す。
    private void WriteIncludeFiles(IReadOnlyList<IncludeFile> includes, string directory)
    {
        foreach (IncludeFile include in includes)
        {
            string path = Path.Combine(directory, include.Name);
            if (File.Exists(path))
            {
                _logger.Warning(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        s_fileAlreadyExistsFormat,
                        include.Name
                    )
                );
                continue;
            }

            _logger.Write(
                string.Format(CultureInfo.InvariantCulture, s_outputtingToFormat, include.Name)
            );
            WriteOutputLines(include.Lines, path);
        }
    }

    private static string BuildAutoIncrementDirName(string parentDir, string baseName)
    {
        string candidate = Path.Combine(parentDir, baseName);
        if (!Directory.Exists(candidate))
        {
            return candidate;
        }

        int i = 1;
        string directoryPath;
        do
        {
            directoryPath = Path.Combine(parentDir, $"{baseName} ({i})");
            i++;
        } while (Directory.Exists(directoryPath));

        return directoryPath;
    }

    private static string BuildAutoIncrementFileName(string dir, string baseName, string extension)
    {
        string candidate = Path.Combine(dir, baseName + extension);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        int i = 1;
        string filePath;
        do
        {
            filePath = Path.Combine(dir, $"{baseName} ({i}){extension}");
            i++;
        } while (File.Exists(filePath));

        return filePath;
    }
}
