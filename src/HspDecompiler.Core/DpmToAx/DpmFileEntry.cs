namespace HspDecompiler.Core.DpmToAx;

/// <summary>
/// <para xml:lang="en">One file stored in a DPMX or DPM2 archive.</para>
/// <para xml:lang="ja">DPMX または DPM2 アーカイブに格納された 1 つのファイル。</para>
/// </summary>
public sealed class DpmFileEntry
{
    /// <summary>
    /// <para xml:lang="en">
    /// Name inside the archive. DPM2 entries in a subdirectory carry it as a backslash-separated prefix.
    /// </para>
    /// <para xml:lang="ja">
    /// アーカイブ内の名前。サブディレクトリにある DPM2 のエントリは、円記号区切りの接頭辞を持つ。
    /// </para>
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// <para xml:lang="en">DPMX entry field of unknown purpose, kept as read.</para>
    /// <para xml:lang="ja">用途不明の DPMX エントリのフィールド。読んだ値をそのまま保持する。</para>
    /// </summary>
    public int Unknown { get; set; }

    /// <summary>
    /// <para xml:lang="en">DPMX encryption key; 0 means the entry is stored in plain form.</para>
    /// <para xml:lang="ja">DPMX の暗号鍵。0 ならエントリは暗号化されていない。</para>
    /// </summary>
    public int EncryptionKey { get; set; }

    /// <summary>
    /// <para xml:lang="en">Offset of the file data from the start of the archive's data section.</para>
    /// <para xml:lang="ja">アーカイブのデータ部の先頭から、ファイルデータまでのオフセット。</para>
    /// </summary>
    public int FileOffset { get; set; }

    /// <summary>
    /// <para xml:lang="en">Size of the file data in bytes.</para>
    /// <para xml:lang="ja">ファイルデータのバイト数。</para>
    /// </summary>
    public int FileSize { get; set; }

    /// <summary>
    /// <para xml:lang="en">DPM2 checksum that seeds the entry's key table; 0 means no DPM2 encryption.</para>
    /// <para xml:lang="ja">エントリの鍵表のシードとなる DPM2 のチェックサム。0 なら DPM2 の暗号化は無い。</para>
    /// </summary>
    public int Checksum { get; set; }

    /// <summary>
    /// <para xml:lang="en">Whether the entry needs DPMX decryption.</para>
    /// <para xml:lang="ja">エントリに DPMX の復号が必要かどうか。</para>
    /// </summary>
    public bool IsEncrypted => EncryptionKey != 0;
}
