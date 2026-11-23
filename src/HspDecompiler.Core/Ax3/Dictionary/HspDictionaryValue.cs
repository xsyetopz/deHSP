namespace HspDecompiler.Core.Ax3.Dictionary;

internal struct HspDictionaryValue
{
    internal HspDictionaryValue(
        string name,
        HspCodeType type,
        HspCodeExtraOptions extra,
        int operatorPriority
    )
    {
        _name = name;
        _type = type;
        _extra = extra;
        _operatorPriority = operatorPriority;
    }

    internal string _name;
    internal HspCodeType _type;
    internal HspCodeExtraOptions _extra;
    internal int _operatorPriority;

    public override readonly string ToString() =>
        _name.Length == 0 ? _type.ToString() : _type.ToString() + "  \"" + _name + "\"";
}
