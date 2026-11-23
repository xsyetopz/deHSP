using System.Collections.Generic;

namespace HspDecompiler.Core.Ax2.Data;

// One HSP 2 object file as Ax2ImageReader reads it: the header, the tables, the token words and
// the text segment. Token reads names and strings back from it while Ax2Emitter writes lines.
//
// Ax2ImageReader が読んだ 1 つの HSP 2 オブジェクトファイル。ヘッダー、各テーブル、トークンの
// ワード、テキストのセグメント。Ax2Emitter が行を書く間、Token はここから名前と文字列を読む。
internal sealed class AxData(
    Header header,
    Label[] labels,
    Dll[] dlls,
    Func[] funcs,
    Deffunc[] deffuncs,
    byte[] tokenData,
    byte[] stringData
)
{
    internal Header Header { get; } = header;

    internal Label[] Labels { get; } = labels;

    internal Dll[] Dlls { get; } = dlls;

    internal Func[] Funcs { get; } = funcs;

    internal Deffunc[] Deffuncs { get; } = deffuncs;

    internal byte[] TokenData { get; } = tokenData;

    internal byte[] StringData { get; } = stringData;

    // The built-ins a #deffunc replaces, whose own calls the source writes as name@hsp.
    //
    // #deffunc が置き換える組み込み命令。それ自身の呼び出しはソースに name@hsp と書く。
    internal HashSet<string> ShadowedBuiltins { get; } = new(System.StringComparer.Ordinal);

    internal string GetString(int offset) => ReadString(offset, StringData);

    internal string? GetDeffuncName(int index) =>
        (index >= Deffuncs.Length) || (index < 0) ? null : Deffuncs[index].Name;

    internal string? GetFuncName(int index) =>
        (index >= Funcs.Length) || (index < 0) ? null : Funcs[index].Name;

    internal bool IsShadowedBuiltin(string name) => ShadowedBuiltins.Contains(name);

    internal static string ReadString(int offset, byte[] dumpData)
    {
        if (offset >= dumpData.Length)
        {
            return "";
        }

        int end = System.Array.IndexOf(dumpData, (byte)0, offset);
        if (end < 0)
        {
            end = dumpData.Length;
        }

        return end <= offset
            ? ""
            : Encoding.ShiftJisHelper.Encoding.GetString(dumpData, offset, end - offset);
    }
}
