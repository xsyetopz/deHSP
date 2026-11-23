namespace HspDecompiler.Core.Pipeline;

/// <summary>
/// <para xml:lang="en">Settings for one <see cref="DecompilerPipeline.RunAsync"/> call.</para>
/// <para xml:lang="ja"><see cref="DecompilerPipeline.RunAsync"/> の 1 回の呼び出しの設定。</para>
/// </summary>
public sealed class DecompilerOptions
{
    /// <summary>
    /// <para xml:lang="en">Path of the .ax, .exe or .dpm file to decompile.</para>
    /// <para xml:lang="ja">デコンパイルする .ax・.exe・.dpm ファイルのパス。</para>
    /// </summary>
    public string InputPath { get; set; } = "";

    /// <summary>
    /// <para xml:lang="en">
    /// Directory for the output. When empty, output goes beside the input, and an archive gets a
    /// new numbered directory named after it.
    /// </para>
    /// <para xml:lang="ja">
    /// 出力先のディレクトリ。空なら入力の隣に出力し、アーカイブはその名前に番号を付けた新しい
    /// ディレクトリへ展開する。
    /// </para>
    /// </summary>
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// <para xml:lang="en">
    /// Whether to decrypt encrypted archive entries. When false, an archive whose entries are all
    /// encrypted yields nothing, and one with some encrypted entries is cancelled unless
    /// <see cref="SkipEncrypted"/> is set.
    /// </para>
    /// <para xml:lang="ja">
    /// 暗号化されたアーカイブのエントリを復号するかどうか。false の場合、全エントリが暗号化された
    /// アーカイブからは何も取り出さず、一部が暗号化されたアーカイブは <see cref="SkipEncrypted"/>
    /// が無ければ展開を中止する。
    /// </para>
    /// </summary>
    public bool AllowDecryption { get; set; } = true;

    /// <summary>
    /// <para xml:lang="en">
    /// Extract only the plain entries and skip the encrypted ones, whatever
    /// <see cref="AllowDecryption"/> says. An archive with no plain entry yields nothing.
    /// </para>
    /// <para xml:lang="ja">
    /// <see cref="AllowDecryption"/> の値にかかわらず、暗号化されていないエントリだけを取り出し、
    /// 暗号化されたエントリを飛ばす。暗号化されていないエントリが無いアーカイブからは何も取り出さない。
    /// </para>
    /// </summary>
    public bool SkipEncrypted { get; set; }
}
