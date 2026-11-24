using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Containers;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Pipeline;

// Extraction of DPMX and DPM2 archives: DpmArchive reads and decrypts the entries in memory,
// and the pipeline writes them out.
//
// DPMX・DPM2 アーカイブの展開。DpmArchive がメモリ上でエントリを読んで復号し、パイプラインが
// それを書き出す。
public sealed partial class DecompilerPipeline
{
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
        ArgumentNullException.ThrowIfNull(outputDir);
        ArgumentNullException.ThrowIfNull(options);

        ArchiveContents contents = await ExtractAsync(
                reader,
                outputDir,
                options,
                _dictionary == null ? null : SettingsOf(options),
                ct
            )
            .ConfigureAwait(false);
        return contents.Listing;
    }

    // Reads the archive and writes each entry read in plain form into outputDir. Without
    // settings, no key can be checked, so every encrypted entry stays unread.
    //
    // アーカイブを読み、平文で読めた各エントリを outputDir へ書く。設定が無ければ鍵を確かめ
    // られないため、暗号化されたエントリはすべて読まずに残る。
    private async Task<ArchiveContents> ExtractAsync(
        BinaryReader reader,
        string outputDir,
        DecompilerOptions options,
        DecodeSettings? settings,
        CancellationToken ct
    )
    {
        var archive = new DpmArchive(
            _logger,
            new KeyCracker(bytes => settings != null && DecodesWhole(bytes, settings))
        );
        ArchiveContents contents = archive.Read(reader, options);
        if (contents.Listing.AllEncrypted || contents.Listing.Cancelled)
        {
            return contents;
        }

        OutputWriter.CreateDirectory(outputDir);
        foreach (ArchiveEntry entry in contents.Entries)
        {
            await _writer.SaveEntryAsync(entry, outputDir, ct).ConfigureAwait(false);
        }

        _logger.Write(Strings.ExtractionComplete);
        return contents;
    }

    // The key oracle: a key counts only when the whole AX decodes, since the magic alone matches
    // many wrong keys. The trial decodes with no common files, so trying a key reads no disk.
    // CrackEncryption takes a synchronous check, so the decode blocks here.
    //
    // 鍵の判定。マジックだけでは誤った鍵も多く一致するため、AX 全体を復元できた鍵だけを採用する。
    // 試行は共通ファイル無しで復元するため、鍵を試してもディスクを読まない。CrackEncryption は
    // 同期の検査を受け取るため、ここでは復元を同期で待つ。
    private bool DecodesWhole(ReadOnlyMemory<byte> bytes, DecodeSettings settings)
    {
        byte[] array = bytes.ToArray();
        if (AxImage.FormatOf(array) is not { } format)
        {
            return false;
        }

        try
        {
            (settings with { CommonFiles = null })
                .DecoderFor(format)
                .DecodeAsync(
                    new AxImage(array, format),
                    "start" + AxFormats.SourceExtension(format),
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
