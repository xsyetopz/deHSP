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

/// <summary>
/// <para xml:lang="en">
/// Decompiles HSP 2 and HSP 3 programs: a compiled .ax file, a DPMX or DPM2 archive, or an
/// executable with an archive inside. Call <see cref="Initialize()"/> once before decoding.
/// </para>
/// <para xml:lang="ja">
/// HSP 2 と HSP 3 のプログラムをデコンパイルする。対象はコンパイル済みの .ax ファイル、DPMX
/// または DPM2 アーカイブ、アーカイブを含む実行ファイル。復元の前に一度
/// <see cref="Initialize()"/> を呼ぶ。
/// </para>
/// </summary>
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

    /// <summary>
    /// <para xml:lang="en">Creates a pipeline that reports to the given logger and progress sink.</para>
    /// <para xml:lang="ja">指定したロガーと進行状況の通知先へ報告するパイプラインを作る。</para>
    /// </summary>
    /// <param name="logger">
    /// <para xml:lang="en">Receives messages and warnings.</para>
    /// <para xml:lang="ja">メッセージと警告を受け取る。</para>
    /// </param>
    /// <param name="progress">
    /// <para xml:lang="en">Receives status text and yields to the host.</para>
    /// <para xml:lang="ja">状態の文字列を受け取り、ホストへ制御を譲る。</para>
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <para xml:lang="en">An argument is null.</para>
    /// <para xml:lang="ja">引数が null。</para>
    /// </exception>
    public DecompilerPipeline(IDecompilerLogger logger, IProgressReporter progress)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(progress);
        _logger = logger;
        _progress = progress;
    }

    /// <summary>
    /// <para xml:lang="en">Loads the HSP keyword dictionary built into the library.</para>
    /// <para xml:lang="ja">ライブラリに組み込まれた HSP のキーワード辞書を読み込む。</para>
    /// </summary>
    /// <returns>
    /// <para xml:lang="en"><see langword="true"/> once the dictionary is ready.</para>
    /// <para xml:lang="ja">辞書の準備ができると <see langword="true"/>。</para>
    /// </returns>
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
    /// <para xml:lang="en">Loads an HSP keyword dictionary from a JSON file in the format of the built-in one.</para>
    /// <para xml:lang="ja">組み込みの辞書と同じ形式の JSON ファイルから HSP のキーワード辞書を読み込む。</para>
    /// </summary>
    /// <param name="dictionaryPath">
    /// <para xml:lang="en">Path of the dictionary JSON file.</para>
    /// <para xml:lang="ja">辞書の JSON ファイルのパス。</para>
    /// </param>
    /// <returns>
    /// <para xml:lang="en">
    /// <see langword="true"/> when the file loaded; <see langword="false"/> when it is missing or
    /// malformed, after logging the failure.
    /// </para>
    /// <para xml:lang="ja">
    /// 読み込めれば <see langword="true"/>。ファイルが無いか形式が不正なら、失敗を記録した上で
    /// <see langword="false"/>。
    /// </para>
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

    /// <summary>
    /// <para xml:lang="en">
    /// Decompiles the input that <paramref name="options"/> names, extracting an archive first when
    /// the input holds one. Failures are logged and returned rather than thrown.
    /// </para>
    /// <para xml:lang="ja">
    /// <paramref name="options"/> が指す入力をデコンパイルする。入力がアーカイブを含む場合は先に
    /// 展開する。失敗は例外にせず、記録した上で結果として返す。
    /// </para>
    /// </summary>
    /// <param name="options">
    /// <para xml:lang="en">Input, output and decryption settings.</para>
    /// <para xml:lang="ja">入力、出力、復号の設定。</para>
    /// </param>
    /// <param name="ct">
    /// <para xml:lang="en">Stops the work when cancelled.</para>
    /// <para xml:lang="ja">キャンセルされると処理を中止する。</para>
    /// </param>
    /// <returns>
    /// <para xml:lang="en">The outcome, including the output path and warnings.</para>
    /// <para xml:lang="ja">出力パスと警告を含む結果。</para>
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <para xml:lang="en"><paramref name="options"/> is null.</para>
    /// <para xml:lang="ja"><paramref name="options"/> が null。</para>
    /// </exception>
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
    //
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
    //
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
    //
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

        // A run that extracts nothing fails, so callers such as the CLI never report success
        // for an archive whose options blocked every entry.
        //
        // 何も展開しなかった処理は失敗とする。設定がすべてのエントリを止めたアーカイブについて、
        // CLI などの呼び出し側が成功と報告しないようにするため。
        if (dpmResult.AllEncrypted || dpmResult.Cancelled)
        {
            decompilerOutput.Success = false;
            decompilerOutput.ErrorMessage = dpmResult.AllEncrypted
                ? Strings.AllFilesEncrypted
                : Strings.SomeFilesEncrypted;
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

    /// <summary>
    /// <para xml:lang="en">
    /// Extracts the DPMX or DPM2 archive that <paramref name="reader"/> holds into
    /// <paramref name="outputDir"/>, decrypting entries as <paramref name="options"/> allows.
    /// Existing files are kept.
    /// </para>
    /// <para xml:lang="ja">
    /// <paramref name="reader"/> が含む DPMX または DPM2 アーカイブを <paramref name="outputDir"/>
    /// へ展開し、<paramref name="options"/> が許す範囲でエントリを復号する。既存のファイルは
    /// 上書きしない。
    /// </para>
    /// </summary>
    /// <param name="reader">
    /// <para xml:lang="en">Reader over an archive or an executable that contains one.</para>
    /// <para xml:lang="ja">アーカイブ、またはアーカイブを含む実行ファイルを読むリーダー。</para>
    /// </param>
    /// <param name="outputDir">
    /// <para xml:lang="en">Directory to extract into; it is created when missing.</para>
    /// <para xml:lang="ja">展開先のディレクトリ。無ければ作成する。</para>
    /// </param>
    /// <param name="options">
    /// <para xml:lang="en">Decryption settings.</para>
    /// <para xml:lang="ja">復号の設定。</para>
    /// </param>
    /// <param name="ct">
    /// <para xml:lang="en">Stops the work when cancelled.</para>
    /// <para xml:lang="ja">キャンセルされると処理を中止する。</para>
    /// </param>
    /// <returns>
    /// <para xml:lang="en">The archive entries and whether encryption stopped the extraction.</para>
    /// <para xml:lang="ja">アーカイブのエントリと、暗号化によって展開が止まったかどうか。</para>
    /// </returns>
    /// <exception cref="HspDecoderException">
    /// <para xml:lang="en">No archive header is found, or the archive is empty.</para>
    /// <para xml:lang="ja">アーカイブのヘッダーが見つからないか、アーカイブが空。</para>
    /// </exception>
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
        if (totalCount == encryptedCount && !DecryptsEntries(options))
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

    // SkipEncrypted wins over AllowDecryption, so the flag skips encrypted entries on its own.
    //
    // SkipEncrypted は AllowDecryption より優先するため、単独でも暗号化されたエントリを飛ばす。
    private static bool DecryptsEntries(DecompilerOptions options) =>
        options is { AllowDecryption: true, SkipEncrypted: false };

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
        if (file.IsEncrypted && !DecryptsEntries(options))
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
        //
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
                //
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

    /// <summary>
    /// <para xml:lang="en">
    /// Decodes the .ax file that <paramref name="reader"/> holds and writes the source to
    /// <paramref name="outputPath"/> in Shift_JIS. HSP 2 include files go beside it.
    /// </para>
    /// <para xml:lang="ja">
    /// <paramref name="reader"/> が含む .ax ファイルを復元し、ソースを Shift_JIS で
    /// <paramref name="outputPath"/> へ書き出す。HSP 2 のインクルードファイルはその隣に置く。
    /// </para>
    /// </summary>
    /// <param name="reader">
    /// <para xml:lang="en">Reader positioned at the start of the .ax file.</para>
    /// <para xml:lang="ja">.ax ファイルの先頭を指すリーダー。</para>
    /// </param>
    /// <param name="outputPath">
    /// <para xml:lang="en">Path of the source file to write.</para>
    /// <para xml:lang="ja">書き出すソースファイルのパス。</para>
    /// </param>
    /// <param name="ct">
    /// <para xml:lang="en">Stops the work when cancelled.</para>
    /// <para xml:lang="ja">キャンセルされると処理を中止する。</para>
    /// </param>
    /// <returns>
    /// <para xml:lang="en">A task that completes when the source is written.</para>
    /// <para xml:lang="ja">ソースを書き終えると完了するタスク。</para>
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// <para xml:lang="en"><see cref="Initialize()"/> has not been called.</para>
    /// <para xml:lang="ja"><see cref="Initialize()"/> がまだ呼ばれていない。</para>
    /// </exception>
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
    //
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
