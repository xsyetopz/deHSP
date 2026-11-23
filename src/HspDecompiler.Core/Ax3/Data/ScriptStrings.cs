using System.Collections.Generic;
using System.Text;
using System.Text.Unicode;
using HspDecompiler.Core.Encoding;

namespace HspDecompiler.Core.Ax3.Data;

// The text hspcmp stores through PutDS: the string literals of the code and the variable names
// of the debug segment. `#cmpopt utf8 1` and the -u and -i switches turn exactly this text into
// UTF-8 (codegen.cpp PutDSStr), while function, DLL and plugin names go through PutDSBuf and keep
// the source encoding, so those names are not collected here.
//
// hspcmp が PutDS で格納するテキスト。コード中の文字列リテラルとデバッグセグメントの変数名。
// `#cmpopt utf8 1` と -u・-i スイッチが UTF-8 に変換するのはまさにこのテキストで
// (codegen.cpp の PutDSStr)、関数・DLL・プラグインの名前は PutDSBuf を通ってソースの
// エンコーディングのまま残るため、ここには集めない。
internal sealed class ScriptStrings
{
    private static readonly UTF8Encoding s_utf8 = new(false);

    private readonly List<byte[]> _texts = [];

    internal System.Text.Encoding Encoding { get; private set; } = ShiftJisHelper.Encoding;

    internal bool IsUtf8 { get; private set; }

    internal byte[] Add(byte[] text)
    {
        _texts.Add(text);
        return text;
    }

    // hspcmp writes boot bits only into an AX with a #runtime, so with a runtime the bits decide
    // and without one the bytes alone do. `#cmpopt utf8 1` sets the UTF-8 bit (hsc3.cpp,
    // codegen.cpp), and hspcmp 3.8 beta 2 stores UTF-8 strings for every HSP64 program even
    // without it, so either bit counts as a UTF-8 runtime. Shift_JIS text with any lead byte from
    // 0x81 to 0x9F is never valid UTF-8, and the rest rarely is, so one invalid string settles the
    // encoding as Shift_JIS.
    //
    // hspcmp は起動ビットを #runtime のある AX にだけ書くため、ランタイムがあればビットで
    // 判断し、無ければバイト列だけで判断する。`#cmpopt utf8 1` は UTF-8 ビットを立て
    // (hsc3.cpp、codegen.cpp)、hspcmp 3.8 beta 2 はそれが無くても HSP64 のプログラムでは
    // 常に UTF-8 の文字列を格納するため、どちらのビットも UTF-8 ランタイムとみなす。
    // 0x81〜0x9F の先頭バイトを含む Shift_JIS は UTF-8 として正しくならず、それ以外も
    // まれなため、不正な文字列が 1 つでもあれば Shift_JIS と決める。
    internal void Resolve(bool hasRuntime, bool utf8Runtime)
    {
        bool anyNonAscii = false;
        foreach (byte[] text in _texts)
        {
            if (Ascii.IsValid(text))
            {
                continue;
            }

            if (!Utf8.IsValid(text))
            {
                return;
            }

            anyNonAscii = true;
        }

        IsUtf8 = anyNonAscii && (utf8Runtime || !hasRuntime);
        if (IsUtf8)
        {
            Encoding = s_utf8;
        }
    }

    internal string Decode(byte[] text) => Escape(Encoding.GetString(text));

    // Writes the text as the body of an HSP string literal. A carriage return is dropped because
    // hspcmp stores "\n" as CR LF and the escape already stands for both.
    //
    // テキストを HSP の文字列リテラルの中身として書く。hspcmp は "\n" を CR LF として格納し、
    // エスケープが両方を表すため、復帰文字は捨てる。
    internal static string Escape(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            _ = c switch
            {
                '\\' => builder.Append(@"\\"),
                '"' => builder.Append("\\\""),
                '\t' => builder.Append(@"\t"),
                '\n' => builder.Append(@"\n"),
                '\r' => builder,
                _ => builder.Append(c),
            };
        }

        return builder.ToString();
    }
}
