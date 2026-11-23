using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HspDecompiler.Core.Abstractions;

/// <summary>
/// <para xml:lang="en">Turns a compiled HSP object file (.ax) back into source lines.</para>
/// <para xml:lang="ja">コンパイル済みの HSP オブジェクトファイル(.ax)をソースの行へ戻す。</para>
/// </summary>
public interface IAxDecoder
{
    /// <summary>
    /// <para xml:lang="en">Decodes the object file that <paramref name="reader"/> is positioned on.</para>
    /// <para xml:lang="ja"><paramref name="reader"/> が指すオブジェクトファイルを復元する。</para>
    /// </summary>
    /// <param name="reader">
    /// <para xml:lang="en">Reader positioned at the start of the object file.</para>
    /// <para xml:lang="ja">オブジェクトファイルの先頭を指すリーダー。</para>
    /// </param>
    /// <param name="logger">
    /// <para xml:lang="en">Receives progress messages and decoding warnings.</para>
    /// <para xml:lang="ja">進行状況のメッセージと復元時の警告を受け取る。</para>
    /// </param>
    /// <param name="progress">
    /// <para xml:lang="en">Receives status text and yields so a UI stays responsive.</para>
    /// <para xml:lang="ja">状態の文字列を受け取り、UI が応答できるよう制御を譲る。</para>
    /// </param>
    /// <param name="ct">
    /// <para xml:lang="en">Stops decoding when cancelled.</para>
    /// <para xml:lang="ja">キャンセルされると復元を中止する。</para>
    /// </param>
    /// <returns>
    /// <para xml:lang="en">The source lines, without line terminators.</para>
    /// <para xml:lang="ja">改行文字を含まないソースの行。</para>
    /// </returns>
    Task<List<string>> DecodeAsync(
        BinaryReader reader,
        IDecompilerLogger logger,
        IProgressReporter progress,
        CancellationToken ct = default
    );
}
