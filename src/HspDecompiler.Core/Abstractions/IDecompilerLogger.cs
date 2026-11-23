using System;
using System.Collections.Generic;

namespace HspDecompiler.Core.Abstractions;

/// <summary>
/// <para xml:lang="en">
/// Receives the messages the decompiler produces. The CLI writes them to the console and the GUI
/// to its log pane.
/// </para>
/// <para xml:lang="ja">
/// デコンパイラが出すメッセージを受け取る。CLI はコンソールへ、GUI はログ欄へ書き出す。
/// </para>
/// </summary>
public interface IDecompilerLogger
{
    /// <summary>
    /// <para xml:lang="en">Records a progress message.</para>
    /// <para xml:lang="ja">進行状況のメッセージを記録する。</para>
    /// </summary>
    /// <param name="message">
    /// <para xml:lang="en">Message text.</para>
    /// <para xml:lang="ja">メッセージの文字列。</para>
    /// </param>
    void Write(string message);

    /// <summary>
    /// <para xml:lang="en">
    /// Records a decoding warning. Warnings also go to the .log file beside the output.
    /// </para>
    /// <para xml:lang="ja">復元時の警告を記録する。警告は出力の隣の .log ファイルにも書かれる。</para>
    /// </summary>
    /// <param name="message">
    /// <para xml:lang="en">Warning text.</para>
    /// <para xml:lang="ja">警告の文字列。</para>
    /// </param>
    /// <param name="lineNumber">
    /// <para xml:lang="en">Output line the warning refers to, or -1 when it has none.</para>
    /// <para xml:lang="ja">警告が指す出力の行番号。該当する行が無ければ -1。</para>
    /// </param>
    void Warning(string message, int lineNumber = -1);

    /// <summary>
    /// <para xml:lang="en">Records an error message.</para>
    /// <para xml:lang="ja">エラーメッセージを記録する。</para>
    /// </summary>
    /// <param name="message">
    /// <para xml:lang="en">Error text.</para>
    /// <para xml:lang="ja">エラーの文字列。</para>
    /// </param>
    void LogError(string message);

    /// <summary>
    /// <para xml:lang="en">Records an exception that stopped the run.</para>
    /// <para xml:lang="ja">処理を止めた例外を記録する。</para>
    /// </summary>
    /// <param name="exception">
    /// <para xml:lang="en">The exception.</para>
    /// <para xml:lang="ja">発生した例外。</para>
    /// </param>
    void LogError(Exception exception);

    /// <summary>
    /// <para xml:lang="en">Indents the messages that follow, until the matching <see cref="EndSection"/>.</para>
    /// <para xml:lang="ja">対応する <see cref="EndSection"/> まで、後続のメッセージを字下げする。</para>
    /// </summary>
    void StartSection();

    /// <summary>
    /// <para xml:lang="en">Ends the indentation that <see cref="StartSection"/> began.</para>
    /// <para xml:lang="ja"><see cref="StartSection"/> で始めた字下げを終える。</para>
    /// </summary>
    void EndSection();

    /// <summary>
    /// <para xml:lang="en">Warnings recorded so far, in order.</para>
    /// <para xml:lang="ja">これまでに記録された警告(記録順)。</para>
    /// </summary>
    IReadOnlyList<string> Warnings { get; }
}
