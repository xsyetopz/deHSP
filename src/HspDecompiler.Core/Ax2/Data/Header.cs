namespace HspDecompiler.Core.Ax2.Data;

internal class Header
{
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
    // The format word hspcmp writes after the magic; every HSP 2.6 and 2.61 build writes 0x206.
    //
    // hspcmp がマジックの後に書く形式のワード。HSP 2.6 と 2.61 のどのビルドも 0x206 を書く。
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

    internal static Header? FromIntArray(int[] data)
    {
        if (data.Length < 20)
        {
            return null;
        }

        var ret = new Header
        {
            _version = (uint)data[1],
            _allDataByte = data[3],
            _scriptOffset = data[4],
            _scriptByte = data[5],
            _textOffset = data[6],
            _textByte = data[7],
            _labelOffset = data[8],
            _labelByte = data[9],

            _dllOffset = data[12],
            _dllByte = data[13],
            _funcOffset = data[14],
            _funcByte = data[15],
            _deffuncOffset = data[16],
            _deffuncByte = data[17],
            _moduleOffset = data[18],
            _moduleByte = data[19],
        };
        return ret;
    }
}
