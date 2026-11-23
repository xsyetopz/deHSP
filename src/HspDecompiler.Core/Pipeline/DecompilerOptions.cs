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

    /// <summary>
    /// <para xml:lang="en">
    /// Write the sources as UTF-8 without a byte order mark instead of Shift_JIS. hspcmp reads
    /// such a file only with its -i or -j switch, so this suits programs whose strings are UTF-8
    /// (the output then holds <c>#cmpopt utf8 1</c>); for Shift_JIS programs, Shift_JIS output
    /// is the one that recompiles to the same bytes.
    /// </para>
    /// <para xml:lang="ja">
    /// ソースを Shift_JIS ではなく BOM 無しの UTF-8 で書き出す。hspcmp はこのファイルを -i か
    /// -j スイッチ付きでしか読めないため、文字列が UTF-8 のプログラム(出力に
    /// <c>#cmpopt utf8 1</c> が入る)に向く。Shift_JIS のプログラムを同じバイト列に再コンパイル
    /// できるのは Shift_JIS の出力である。
    /// </para>
    /// </summary>
    public bool Utf8Output { get; set; }

    /// <summary>
    /// <para xml:lang="en">
    /// Directory of the HSP common files (the one hspcmp --compath names). A debug build records
    /// each #include it read; a common header that left no code, such as hspcmp.as, gets its
    /// #include back when this directory holds a copy whose declarations match the program's
    /// imports. When empty, such an #include is written as a comment.
    /// </para>
    /// <para xml:lang="ja">
    /// HSP の共通ファイルのディレクトリ(hspcmp の --compath が指すもの)。デバッグビルドは
    /// 読んだ #include をそれぞれ記録する。hspcmp.as のようにコードを残さなかった共通ヘッダーは、
    /// このディレクトリに宣言がプログラムのインポートと一致するコピーがあれば #include を
    /// 復元する。空ならその #include はコメントとして書き出す。
    /// </para>
    /// </summary>
    public string? CommonDirectory { get; set; }

    /// <summary>
    /// <para xml:lang="en">
    /// How the HSP 3 source spells constructs that compile to the same bytes either way.
    /// </para>
    /// <para xml:lang="ja">
    /// どちらでも同じバイト列にコンパイルされる構文を、HSP 3 のソースでどう綴るか。
    /// </para>
    /// </summary>
    public SourceStyle Style { get; set; } = new();
}
