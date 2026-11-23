using System.Threading;
using System.Threading.Tasks;

namespace HspDecompiler.Core.Abstractions;

/// <summary>
/// <para xml:lang="en">Shows decompilation progress and keeps a UI thread responsive during long runs.</para>
/// <para xml:lang="ja">デコンパイルの進行状況を示し、長い処理の間も UI スレッドを応答可能に保つ。</para>
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// <para xml:lang="en">Shows the current stage.</para>
    /// <para xml:lang="ja">現在の段階を表示する。</para>
    /// </summary>
    /// <param name="status">
    /// <para xml:lang="en">Short status text.</para>
    /// <para xml:lang="ja">短い状態の文字列。</para>
    /// </param>
    void Report(string status);

    /// <summary>
    /// <para xml:lang="en">
    /// Gives the host a chance to process pending work. Hosts without a UI return a completed task.
    /// </para>
    /// <para xml:lang="ja">
    /// ホストに保留中の処理を実行させる機会を与える。UI を持たないホストは完了済みのタスクを返す。
    /// </para>
    /// </summary>
    /// <param name="ct">
    /// <para xml:lang="en">Cancels the wait.</para>
    /// <para xml:lang="ja">待機をキャンセルする。</para>
    /// </param>
    /// <returns>
    /// <para xml:lang="en">A task that completes when the decompiler may continue.</para>
    /// <para xml:lang="ja">デコンパイラが処理を続けてよい時に完了するタスク。</para>
    /// </returns>
    Task YieldAsync(CancellationToken ct = default);
}
