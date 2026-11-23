using System.Globalization;

namespace HspDecompiler.Core.Ax2.Data;

internal class Label
{
    internal Label(int index, int tokenIndex)
    {
        _tokenIndex = tokenIndex;
        _name = "*label_" + index.ToString(CultureInfo.InvariantCulture);
    }

    private readonly int _tokenIndex;
    private int _loadCount;
    private string _name;
    private bool _enabled;
    private int _deffunc = -1;

    internal string Name
    {
        get => _name;
        set => _name = value;
    }

    internal int TokenIndex => _tokenIndex;

    internal int Deffunc
    {
        get => _deffunc;
        set => _deffunc = value;
    }

    internal int LoadCount
    {
        get => _loadCount;
        set => _loadCount = value;
    }

    internal bool IsCompilerGenerated { get; set; }

    internal bool Enabled
    {
        get => _deffunc != -1 || _enabled;
        set => _enabled = value;
    }
}
