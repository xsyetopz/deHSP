using System.Text;

namespace HspDecompiler.Core.Ax3.Data.PP;

internal class Runtime : Preprocessor
{
    internal Runtime(string theName)
    {
        _name = theName;
    }

    private readonly string? _name;

    internal string? Name => _name;

    public override string ToString()
    {
        var strbd = new StringBuilder();
        strbd.Append("#runtime ");
        strbd.Append('"');
        strbd.Append(_name);
        strbd.Append('"');
        return strbd.ToString();
    }
}
