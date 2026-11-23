using System;

namespace HspDecompiler.Core.Exceptions;

/// <summary>
/// <para xml:lang="en">
/// Thrown when an input cannot be decoded: an unknown format, a damaged archive or object file,
/// or a failed read.
/// </para>
/// <para xml:lang="ja">
/// 入力を復元できない時に投げられる。未知の形式、壊れたアーカイブやオブジェクトファイル、
/// 読み込みの失敗などが該当する。
/// </para>
/// </summary>
public sealed class HspDecoderException : Exception
{
    /// <summary>
    /// <para xml:lang="en">Creates the exception with the default message.</para>
    /// <para xml:lang="ja">既定のメッセージで例外を作る。</para>
    /// </summary>
    public HspDecoderException()
        : base() { }

    /// <summary>
    /// <para xml:lang="en">Creates the exception with a message.</para>
    /// <para xml:lang="ja">メッセージを指定して例外を作る。</para>
    /// </summary>
    /// <param name="message">
    /// <para xml:lang="en">Description of the failure.</para>
    /// <para xml:lang="ja">失敗の説明。</para>
    /// </param>
    public HspDecoderException(string message)
        : base(message) { }

    /// <summary>
    /// <para xml:lang="en">Creates the exception with the name of the failing component and a message.</para>
    /// <para xml:lang="ja">失敗した部分の名前とメッセージを指定して例外を作る。</para>
    /// </summary>
    /// <param name="source">
    /// <para xml:lang="en">Name of the failing component, stored in <see cref="Exception.Source"/>.</para>
    /// <para xml:lang="ja"><see cref="Exception.Source"/> に格納する、失敗した部分の名前。</para>
    /// </param>
    /// <param name="message">
    /// <para xml:lang="en">Description of the failure.</para>
    /// <para xml:lang="ja">失敗の説明。</para>
    /// </param>
    public HspDecoderException(string source, string message)
        : base(message)
    {
        Source = source;
    }

    /// <summary>
    /// <para xml:lang="en">Creates the exception with a message and the exception that caused it.</para>
    /// <para xml:lang="ja">メッセージと原因の例外を指定して例外を作る。</para>
    /// </summary>
    /// <param name="message">
    /// <para xml:lang="en">Description of the failure.</para>
    /// <para xml:lang="ja">失敗の説明。</para>
    /// </param>
    /// <param name="innerException">
    /// <para xml:lang="en">The exception that caused the failure.</para>
    /// <para xml:lang="ja">失敗の原因となった例外。</para>
    /// </param>
    public HspDecoderException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>
    /// <para xml:lang="en">
    /// Creates the exception with the name of the failing component, a message, and the exception
    /// that caused it.
    /// </para>
    /// <para xml:lang="ja">失敗した部分の名前、メッセージ、原因の例外を指定して例外を作る。</para>
    /// </summary>
    /// <param name="source">
    /// <para xml:lang="en">Name of the failing component, stored in <see cref="Exception.Source"/>.</para>
    /// <para xml:lang="ja"><see cref="Exception.Source"/> に格納する、失敗した部分の名前。</para>
    /// </param>
    /// <param name="message">
    /// <para xml:lang="en">Description of the failure.</para>
    /// <para xml:lang="ja">失敗の説明。</para>
    /// </param>
    /// <param name="innerException">
    /// <para xml:lang="en">The exception that caused the failure.</para>
    /// <para xml:lang="ja">失敗の原因となった例外。</para>
    /// </param>
    public HspDecoderException(string source, string message, Exception innerException)
        : base(message, innerException)
    {
        Source = source;
    }

    /// <summary>
    /// <para xml:lang="en">Copies the message, cause and source of another instance.</para>
    /// <para xml:lang="ja">別のインスタンスのメッセージ、原因、発生元を複製する。</para>
    /// </summary>
    /// <param name="other">
    /// <para xml:lang="en">Instance to copy.</para>
    /// <para xml:lang="ja">複製元のインスタンス。</para>
    /// </param>
    public HspDecoderException(HspDecoderException other)
        : base(other.Message, other.InnerException)
    {
        Source = other.Source;
    }
}
