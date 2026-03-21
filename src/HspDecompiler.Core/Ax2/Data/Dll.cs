namespace HspDecompiler.Core.Ax2.Data;

internal struct Dll
{
    private string _name;

    internal string Name
    {
        readonly get => _name;
        set => _name = value;
    }

    public override readonly string ToString() => "#uselib " + "\"" + _name + "\"";
}
