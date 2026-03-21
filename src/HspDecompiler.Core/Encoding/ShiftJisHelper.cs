using System.Text;

namespace HspDecompiler.Core.Encoding;

public static class ShiftJisHelper
{
    private static System.Text.Encoding? s_encoding;
    private static readonly object s_lock = new();

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
