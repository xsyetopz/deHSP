using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax3;
using HspDecompiler.Core.Containers;
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
public sealed partial class DecompilerPipeline
{
    private static readonly CompositeFormat s_dictionaryLoadSuccessFormat = CompositeFormat.Parse(
        Strings.DictionaryLoadSuccess
    );

    private static readonly CompositeFormat s_dictionaryLoadFailedFormat = CompositeFormat.Parse(
        Strings.DictionaryLoadFailed
    );

    private static readonly CompositeFormat s_readingFileFormat = CompositeFormat.Parse(
        Strings.ReadingFile
    );

    private static readonly UTF8Encoding s_utf8 = new(false);

    private readonly IDecompilerLogger _logger;
    private readonly IProgressReporter _progress;
    private readonly OutputWriter _writer;
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
        _writer = new OutputWriter(logger);
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
                "Dictionary.xml " + _dictionary.Version
            )
        );
        return true;
    }

    /// <summary>
    /// <para xml:lang="en">
    /// Loads an HSP keyword dictionary from an XML file that follows Dictionary.xsd, the schema
    /// of the built-in dictionary.
    /// </para>
    /// <para xml:lang="ja">
    /// 組み込みの辞書のスキーマ Dictionary.xsd に従う XML ファイルから HSP のキーワード辞書を
    /// 読み込む。
    /// </para>
    /// </summary>
    /// <param name="dictionaryPath">
    /// <para xml:lang="en">Path of the dictionary XML file.</para>
    /// <para xml:lang="ja">辞書の XML ファイルのパス。</para>
    /// </param>
    /// <returns>
    /// <para xml:lang="en">
    /// <see langword="true"/> when the file loaded; <see langword="false"/> when it is missing,
    /// unreadable, or fails the schema, after logging the reason.
    /// </para>
    /// <para xml:lang="ja">
    /// 読み込めれば <see langword="true"/>。ファイルが無い、読めない、またはスキーマに合わない
    /// 場合は、理由を記録した上で <see langword="false"/>。
    /// </para>
    /// </returns>
    public bool Initialize(string dictionaryPath)
    {
        string name = Path.GetFileName(dictionaryPath);
        try
        {
            _dictionary = Hsp3Dictionary.FromFile(dictionaryPath);
        }
        catch (Exception ex)
            when (ex
                    is IOException
                        or UnauthorizedAccessException
                        or XmlException
                        or XmlSchemaException
                        or InvalidDataException
            )
        {
            _dictionary = null;
            _logger.Write(
                string.Format(
                    CultureInfo.InvariantCulture,
                    s_dictionaryLoadFailedFormat,
                    name,
                    ex.Message
                )
            );
            return false;
        }

        _logger.Write(
            string.Format(CultureInfo.InvariantCulture, s_dictionaryLoadSuccessFormat, name)
        );
        return true;
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

            _writer.WriteWarningLog(errorLogPath, decompilerOutput);
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
        string magic = AxFormats.Peek(reader);
        string? explicitDir = string.IsNullOrEmpty(options.OutputDirectory)
            ? null
            : Path.TrimEndingDirectorySeparator(options.OutputDirectory)
                + Path.DirectorySeparatorChar;

        if (AxFormats.Of(magic) is { } format)
        {
            if (explicitDir != null)
            {
                Directory.CreateDirectory(explicitDir);
            }

            string outputPath = OutputWriter.FreeFileName(
                explicitDir ?? InputDirectory(options),
                InputBaseName(options),
                AxFormats.SourceExtension(format)
            );
            decompilerOutput.OutputPath = outputPath;
            AxImage image = new(
                Path.GetFileName(options.InputPath),
                reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position)),
                format
            );
            await DecodeImageAsync(
                    image,
                    outputPath,
                    SettingsOf(options),
                    SourceEncoding(options),
                    ct
                )
                .ConfigureAwait(false);
            return outputPath + ".log";
        }

        if (!AxFormats.IsContainer(magic))
        {
            throw new HspDecoderException(Strings.UnrecognizedFileFormat);
        }

        string outputDir = explicitDir
            ?? OutputWriter.FreeDirectoryName(InputDirectory(options), InputBaseName(options));
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

    private static System.Text.Encoding SourceEncoding(DecompilerOptions options) =>
        options.Utf8Output ? s_utf8 : ShiftJisHelper.Encoding;

    private static string InputDirectory(DecompilerOptions options) =>
        (Path.GetDirectoryName(options.InputPath) ?? ".") + Path.DirectorySeparatorChar;

    private static string InputBaseName(DecompilerOptions options) =>
        Path.GetFileNameWithoutExtension(options.InputPath);

    // Extracts the archive and decodes each AX entry from the bytes read out of the archive, so
    // a file of the same name already in the output directory is never decoded in its place.
    // Returns false when nothing past the extraction is left to decompile.
    //
    // アーカイブを展開し、各 AX のエントリをアーカイブから読んだバイト列のまま復元する。その
    // ため出力先に同名のファイルが既にあっても、それを代わりに復元することは無い。展開後に
    // デコンパイルするものが無い場合は false を返す。
    private async Task<bool> DecompileArchiveAsync(
        BinaryReader reader,
        string outputDir,
        DecompilerOptions options,
        DecompilerResult decompilerOutput,
        CancellationToken ct
    )
    {
        outputDir = Path.TrimEndingDirectorySeparator(outputDir) + Path.DirectorySeparatorChar;
        DecodeSettings settings = SettingsOf(options);
        ArchiveContents contents = await ExtractAsync(reader, outputDir, options, settings, ct)
            .ConfigureAwait(false);
        DpmExtractionResult listing = contents.Listing;
        decompilerOutput.DpmFiles.AddRange(listing.Files);
        decompilerOutput.OutputPath = outputDir;

        // A run that extracts nothing fails, so callers such as the CLI never report success
        // for an archive whose options blocked every entry.
        //
        // 何も展開しなかった処理は失敗とする。設定がすべてのエントリを止めたアーカイブについて、
        // CLI などの呼び出し側が成功と報告しないようにするため。
        if (listing.AllEncrypted || listing.Cancelled)
        {
            decompilerOutput.Success = false;
            decompilerOutput.ErrorMessage = listing.AllEncrypted
                ? Strings.AllFilesEncrypted
                : Strings.SomeFilesEncrypted;
            return false;
        }

        foreach (ArchiveEntry entry in contents.Entries)
        {
            if (!entry.IsAx || AxImage.FormatOf(entry.Bytes) is not { } format)
            {
                continue;
            }

            string outputPath = OutputWriter.FreeFileName(
                outputDir,
                Path.GetFileNameWithoutExtension(entry.File.FileName!),
                AxFormats.SourceExtension(format)
            );
            await DecodeImageAsync(
                    new AxImage(entry.File.FileName!, entry.Bytes, format),
                    outputPath,
                    settings,
                    SourceEncoding(options),
                    ct
                )
                .ConfigureAwait(false);
        }

        return true;
    }

    // The settings one run decodes with. The dictionary is required here rather than when the
    // pipeline is built, because Initialize may still pick another one.
    //
    // 1 回の実行が復元に使う設定。Initialize が別の辞書を選び得るため、辞書はパイプラインを
    // 作る時ではなくここで必要とする。
    private DecodeSettings SettingsOf(DecompilerOptions options) =>
        new(
            _dictionary ?? throw new InvalidOperationException(Strings.DictionaryNotInitialized),
            options.Style,
            DirectoryCommonFiles.Of(options.CommonDirectory),
            string.IsNullOrWhiteSpace(options.CompilerRelease)
                ? null
                : options.CompilerRelease.Trim()
        );

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
    public Task DecodeAsync(
        BinaryReader reader,
        string outputPath,
        CancellationToken ct = default
    ) => DecodeAsync(reader, outputPath, ShiftJisHelper.Encoding, ct);

    /// <summary>
    /// <para xml:lang="en">
    /// Decodes the .ax file that <paramref name="reader"/> holds and writes the source to
    /// <paramref name="outputPath"/> in <paramref name="sourceEncoding"/>. HSP 2 include files go
    /// beside it. Each line the encoding cannot hold is reported as a warning.
    /// </para>
    /// <para xml:lang="ja">
    /// <paramref name="reader"/> が含む .ax ファイルを復元し、ソースを
    /// <paramref name="sourceEncoding"/> で <paramref name="outputPath"/> へ書き出す。HSP 2 の
    /// インクルードファイルはその隣に置く。エンコーディングが表せない行はそれぞれ警告する。
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
    /// <param name="sourceEncoding">
    /// <para xml:lang="en">Encoding of the source and include files.</para>
    /// <para xml:lang="ja">ソースとインクルードファイルのエンコーディング。</para>
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
        System.Text.Encoding sourceEncoding,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(outputPath);
        ArgumentNullException.ThrowIfNull(sourceEncoding);

        DecodeSettings settings = SettingsOf(new DecompilerOptions());
        byte[] bytes = reader.ReadBytes(
            (int)(reader.BaseStream.Length - reader.BaseStream.Position)
        );
        await DecodeImageAsync(
                AxImage.Of(Path.GetFileName(outputPath), bytes),
                outputPath,
                settings,
                sourceEncoding,
                ct
            )
            .ConfigureAwait(false);
    }

    private async Task DecodeImageAsync(
        AxImage image,
        string outputPath,
        DecodeSettings settings,
        System.Text.Encoding sourceEncoding,
        CancellationToken ct
    )
    {
        _logger.StartSection();
        _logger.Write(Strings.Decompiling);
        _logger.StartSection();

        DecodedSource source = await settings
            .DecoderFor(image.Format)
            .DecodeAsync(image, Path.GetFileName(outputPath), _logger, _progress, ct)
            .ConfigureAwait(false);

        _logger.EndSection();
        _logger.Write(Strings.DecompileComplete);
        _logger.EndSection();
        _writer.WriteSource(source, outputPath, sourceEncoding);
    }
}
