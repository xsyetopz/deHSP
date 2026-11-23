using System.Collections.Generic;
using HspDecompiler.Core.DpmToAx;

namespace HspDecompiler.Core.Pipeline;

/// <summary>
/// <para xml:lang="en">Outcome of one <see cref="DecompilerPipeline.RunAsync"/> call.</para>
/// <para xml:lang="ja"><see cref="DecompilerPipeline.RunAsync"/> の 1 回の呼び出しの結果。</para>
/// </summary>
public sealed class DecompilerResult
{
    /// <summary>
    /// <para xml:lang="en">Whether the run finished without an error.</para>
    /// <para xml:lang="ja">エラー無く処理を終えたかどうか。</para>
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// <para xml:lang="en">Path of the written source file, or null when none was written.</para>
    /// <para xml:lang="ja">書き出したソースファイルのパス。書き出していなければ null。</para>
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// <para xml:lang="en">Warnings recorded while decoding.</para>
    /// <para xml:lang="ja">復元中に記録された警告。</para>
    /// </summary>
    public List<string> Warnings { get; } = [];

    /// <summary>
    /// <para xml:lang="en">Entries of the input archive; empty for a plain .ax input.</para>
    /// <para xml:lang="ja">入力アーカイブのエントリ。.ax を直接入力した場合は空。</para>
    /// </summary>
    public List<DpmFileEntry> DpmFiles { get; } = [];

    /// <summary>
    /// <para xml:lang="en">Message of the error that stopped the run, or null.</para>
    /// <para xml:lang="ja">処理を止めたエラーのメッセージ。無ければ null。</para>
    /// </summary>
    public string? ErrorMessage { get; set; }
}
