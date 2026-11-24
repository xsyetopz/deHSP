using System;

namespace HspDecompiler.Core.Ax2.Data;

internal class Header
{
    internal const int ShortHeaderWords = 16;

    private Header() { }

    #region var
    private uint _version;
    private int _allDataByte;
    private int _scriptOffset;
    private int _scriptByte;
    private int _textOffset;
    private int _textByte;
    private int _labelOffset;
    private int _labelByte;
    private int _dllOffset;
    private int _dllByte;
    private int _funcOffset;
    private int _funcByte;
    private int _deffuncOffset;
    private int _deffuncByte;
    private int _moduleOffset;
    private int _moduleByte;
    #endregion

    #region properties
    // The format word hspcmp writes after the magic, such as 0x203 for 2.4 and 0x206 for 2.6.
    //
    // hspcmp がマジックの後に書く形式のワード。2.4 は 0x203、2.6 は 0x206 などを書く。
    internal uint Version => _version;

    internal int AllDataByte => _allDataByte;

    internal int ScriptOffset => _scriptOffset;

    internal int ScriptByte => _scriptByte;

    internal int TextOffset => _textOffset;

    internal int TextByte => _textByte;

    internal int LabelOffset => _labelOffset;

    internal int LabelByte => _labelByte;

    internal int DllOffset => _dllOffset;

    internal int DllByte => _dllByte;

    internal int FuncOffset => _funcOffset;

    internal int FuncByte => _funcByte;

    internal int DeffuncOffset => _deffuncOffset;

    internal int DeffuncByte => _deffuncByte;

    internal int ModuleOffset => _moduleOffset;

    internal int ModuleByte => _moduleByte;

    internal int ScriptCount => _scriptByte / 2;

    internal int ScriptEndOffset => _scriptOffset + _scriptByte;

    internal int LabelCount => _labelByte / 4;

    internal int DllCount => _dllByte / 24;

    internal int FuncCount => _funcByte / 16;

    internal int DeffuncCount => _deffuncByte / 16;

    internal int ModuleCount => _moduleByte / 24;

    #endregion

    // hspcmp 2.4 writes a 64-byte header with no #deffunc or #module tables; its code starts where
    // the later header holds them. A field at or past the script offset is code, so it reads as an
    // empty table.
    //
    // hspcmp 2.4 は #deffunc と #module のテーブルが無い 64 バイトのヘッダーを書き、後のヘッダーが
    // それらを持つ位置からコードが始まる。スクリプトのオフセット以降のフィールドはコードなので、
    // 空のテーブルとして読む。
    internal static Header? FromIntArray(int[] data)
    {
        if (data.Length < ShortHeaderWords)
        {
            return null;
        }

        int words = Math.Min(data.Length, data[4] / 4);
        int Field(int index) => index < words ? data[index] : 0;
        return new Header
        {
            _version = (uint)data[1],
            _allDataByte = data[3],
            _scriptOffset = data[4],
            _scriptByte = data[5],
            _textOffset = data[6],
            _textByte = data[7],
            _labelOffset = data[8],
            _labelByte = data[9],
            _dllOffset = Field(12),
            _dllByte = Field(13),
            _funcOffset = Field(14),
            _funcByte = Field(15),
            _deffuncOffset = Field(16),
            _deffuncByte = Field(17),
            _moduleOffset = Field(18),
            _moduleByte = Field(19),
        };
    }
}
