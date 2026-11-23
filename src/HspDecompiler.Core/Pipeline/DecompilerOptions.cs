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
    /// Path of the dictionary the host loaded. The pipeline does not read it; load a dictionary
    /// with <see cref="DecompilerPipeline.Initialize(string)"/>.
    /// </para>
    /// <para xml:lang="ja">
    /// ホストが読み込んだ辞書のパス。パイプラインはこの値を読まない。辞書は
    /// <see cref="DecompilerPipeline.Initialize(string)"/> で読み込む。
    /// </para>
    /// </summary>
    public string? DictionaryPath { get; set; }

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
    /// When <see cref="AllowDecryption"/> is false, extract the plain entries and skip the
    /// encrypted ones instead of cancelling.
    /// </para>
    /// <para xml:lang="ja">
    /// <see cref="AllowDecryption"/> が false の時、展開を中止せずに暗号化されていないエントリだけを
    /// 取り出し、暗号化されたエントリを飛ばす。
    /// </para>
    /// </summary>
    public bool SkipEncrypted { get; set; }

    /// <summary>
    /// <para xml:lang="en">
    /// Whether the host wants detailed logging. The pipeline does not read it; the CLI uses it to
    /// print warnings as they occur.
    /// </para>
    /// <para xml:lang="ja">
    /// ホストが詳細なログを求めるかどうか。パイプラインはこの値を読まない。CLI は警告を発生時に
    /// 表示するために使う。
    /// </para>
    /// </summary>
    public bool Verbose { get; set; }
}
