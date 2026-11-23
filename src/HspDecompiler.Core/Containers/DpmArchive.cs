using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.DpmToAx;
using HspDecompiler.Core.DpmToAx.Crypto;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Pipeline;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Containers;

// One archive entry with its plain bytes.
//
// 平文のバイト列を持つ 1 つのアーカイブのエントリ。
internal sealed record ArchiveEntry(DpmFileEntry File, byte[] Bytes)
{
    internal bool IsAx =>
        File.FileName?.EndsWith(".ax", StringComparison.OrdinalIgnoreCase) == true;
}

// What an archive held: the listing the caller reports, and the entries read in plain form. An
// entry that stays encrypted or cannot be read is listed but has no plain form.
//
// アーカイブが持っていたもの。呼び出し側が報告する一覧と、平文で読めたエントリ。暗号化された
// ままのエントリや読めないエントリは一覧に載るが、平文を持たない。
internal sealed record ArchiveContents(
    DpmExtractionResult Listing,
    IReadOnlyList<ArchiveEntry> Entries
);

// Reads a DPMX or DPM2 archive, alone or inside an executable, into memory: it finds the
// archive (DpmExtractor), undoes the DPM2 layer, and cracks the per-entry encryption when the
// options allow it. It writes nothing; the pipeline decides where the entries go.
//
// 単体または実行ファイル内の DPMX・DPM2 アーカイブをメモリへ読む。アーカイブを探し
// (DpmExtractor)、DPM2 の層を外し、設定が許せばエントリごとの暗号を解く。何も書き出さず、
// エントリの行き先はパイプラインが決める。
internal sealed class DpmArchive(IDecompilerLogger logger, KeyCracker cracker)
{
    private static readonly CompositeFormat s_fileEncryptedFormat = CompositeFormat.Parse(
        Strings.FileEncrypted
    );

    private static readonly CompositeFormat s_fileSeekFailedFormat = CompositeFormat.Parse(
        Strings.FileSeekFailed
    );

    private static readonly CompositeFormat s_decryptingFileFormat = CompositeFormat.Parse(
        Strings.DecryptingFile
    );

    private static readonly CompositeFormat s_decryptionFailedFormat = CompositeFormat.Parse(
        Strings.DecryptionFailed
    );

    internal ArchiveContents Read(BinaryReader reader, DecompilerOptions options)
    {
        var listing = new DpmExtractionResult();

        logger.Write(Strings.SearchingDpmHeader);
        DpmExtractor extractor = DpmExtractor.FromBinaryReader(reader)
            ?? throw new HspDecoderException(Strings.DpmHeaderNotFound);
        if (extractor.FileList == null || extractor.FileList.Count == 0)
        {
            throw new HspDecoderException(Strings.DpmNoFiles);
        }

        listing.Files.AddRange(extractor.FileList);
        int encryptedCount = extractor.FileList.Count(file => file.IsEncrypted);
        listing.EncryptedCount = encryptedCount;

        if (encryptedCount == extractor.FileList.Count && !DecryptsEntries(options))
        {
            listing.AllEncrypted = true;
            logger.Write(Strings.ExtractionCancelled);
            return new ArchiveContents(listing, []);
        }

        if (encryptedCount > 0 && options is { SkipEncrypted: false, AllowDecryption: false })
        {
            listing.Cancelled = true;
            logger.Write(Strings.ExtractionCancelled);
            return new ArchiveContents(listing, []);
        }

        Dpm2CryptoTransform? dpm2 =
            extractor.IsDpm2 && extractor.CrcSeed != 0
                ? Dpm2CryptoTransform.FromCrcSeed(extractor.CrcSeed, extractor.Salt)
                : null;
        List<ArchiveEntry> entries = [];
        foreach (DpmFileEntry file in extractor.FileList)
        {
            if (ReadEntry(reader, extractor, file, options, dpm2) is { } bytes)
            {
                entries.Add(new ArchiveEntry(file, bytes));
            }
        }

        return new ArchiveContents(listing, entries);
    }

    // SkipEncrypted wins over AllowDecryption, so the flag skips encrypted entries on its own.
    //
    // SkipEncrypted は AllowDecryption より優先するため、単独でも暗号化されたエントリを飛ばす。
    private static bool DecryptsEntries(DecompilerOptions options) =>
        options is { AllowDecryption: true, SkipEncrypted: false };

    private byte[]? ReadEntry(
        BinaryReader reader,
        DpmExtractor extractor,
        DpmFileEntry file,
        DecompilerOptions options,
        Dpm2CryptoTransform? dpm2
    )
    {
        if (file.IsEncrypted && !DecryptsEntries(options))
        {
            Log(s_fileEncryptedFormat, file);
            return null;
        }

        if (!extractor.Seek(file))
        {
            Log(s_fileSeekFailedFormat, file);
            return null;
        }

        byte[] bytes = reader.ReadBytes(file.FileSize);
        if (dpm2 != null && file.Checksum != 0)
        {
            bytes = dpm2.Decrypt(bytes, file.Checksum);
        }

        if (!file.IsEncrypted)
        {
            return bytes;
        }

        Log(s_decryptingFileFormat, file);
        byte[]? plain = cracker.Decrypt(bytes);
        if (plain == null)
        {
            Log(s_decryptionFailedFormat, file);
        }

        return plain;
    }

    private void Log(CompositeFormat format, DpmFileEntry file) =>
        logger.Write(string.Format(CultureInfo.InvariantCulture, format, file.FileName));
}
