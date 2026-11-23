using System.Collections.Generic;
using HspDecompiler.Core.DpmToAx;

namespace HspDecompiler.Core.Pipeline;

/// <summary>
/// <para xml:lang="en">Outcome of extracting a DPMX or DPM2 archive.</para>
/// <para xml:lang="ja">DPMX または DPM2 アーカイブを展開した結果。</para>
/// </summary>
public sealed class DpmExtractionResult
{
    /// <summary>
    /// <para xml:lang="en">Every entry in the archive, extracted or not.</para>
    /// <para xml:lang="ja">展開したかどうかに関わらず、アーカイブ内のすべてのエントリ。</para>
    /// </summary>
    public List<DpmFileEntry> Files { get; set; } = [];

    /// <summary>
    /// <para xml:lang="en">Number of entries that need DPMX decryption.</para>
    /// <para xml:lang="ja">DPMX の復号が必要なエントリの数。</para>
    /// </summary>
    public int EncryptedCount { get; set; }

    /// <summary>
    /// <para xml:lang="en">
    /// Every entry is encrypted and the options forbid decryption or skip encrypted entries, so
    /// nothing was extracted.
    /// </para>
    /// <para xml:lang="ja">
    /// 全エントリが暗号化されており、設定が復号を禁止するか暗号化されたエントリを飛ばすため、
    /// 何も展開しなかった。
    /// </para>
    /// </summary>
    public bool AllEncrypted { get; set; }

    /// <summary>
    /// <para xml:lang="en">
    /// Extraction stopped because some entries are encrypted and the options allowed neither
    /// decrypting nor skipping them.
    /// </para>
    /// <para xml:lang="ja">
    /// 一部のエントリが暗号化されており、設定が復号も読み飛ばしも許可しないため展開を中止した。
    /// </para>
    /// </summary>
    public bool Cancelled { get; set; }
}
