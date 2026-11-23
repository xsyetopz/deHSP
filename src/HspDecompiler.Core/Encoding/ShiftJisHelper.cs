using System.Text;
using System.Threading;

namespace HspDecompiler.Core.Encoding;

/// <summary>
/// <para xml:lang="en">
/// Provides the Shift_JIS encoding that HSP object files and the default source output use.
/// </para>
/// <para xml:lang="ja">HSP のオブジェクトファイルと既定のソース出力が使う Shift_JIS エンコーディングを提供する。</para>
/// </summary>
public static class ShiftJisHelper
{
    private static System.Text.Encoding? s_encoding;
    private static readonly Lock s_lock = new();

    /// <summary>
    /// <para xml:lang="en">
    /// The Shift_JIS encoding. The first access registers the code-page provider, which .NET
    /// needs before it can return Shift_JIS.
    /// </para>
    /// <para xml:lang="ja">
    /// Shift_JIS エンコーディング。.NET が Shift_JIS を返すにはコードページプロバイダーが必要な
    /// ため、最初のアクセスで登録する。
    /// </para>
    /// </summary>
    public static System.Text.Encoding Encoding
    {
        get
        {
            if (s_encoding == null)
            {
                lock (s_lock)
                {
                    if (s_encoding == null)
                    {
                        System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                        s_encoding = System.Text.Encoding.GetEncoding("SHIFT-JIS");
                    }
                }
            }

            return s_encoding;
        }
    }
}
