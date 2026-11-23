using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.DpmToAx;
using HspDecompiler.Core.DpmToAx.Crypto;
using HspDecompiler.Core.Encoding;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Pipeline;

// Extraction of DPMX and DPM2 archives, including the decryption of their entries.
//
// DPMX・DPM2 アーカイブの展開と、そのエントリの復号。
public sealed partial class DecompilerPipeline
{
    private static readonly CompositeFormat s_directoryCreateFailedFormat = CompositeFormat.Parse(
        Strings.DirectoryCreateFailed
    );

    private static readonly CompositeFormat s_fileEncryptedFormat = CompositeFormat.Parse(
        Strings.FileEncrypted
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
}
