using System;
using System.Globalization;
using System.Text;
using HspDecompiler.Core.Ax3.Dictionary;
using HspDecompiler.Core.Pipeline;
using HspDecompiler.Core.Releases;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax3.Data;

internal class PrimitiveTokenDataSet
{
    internal AxData? _parent;
    internal int _tokenOffset;
    internal int _type;
    internal int _flag;
    internal int _value;
    internal string? _name;
    internal HspDictionaryValue _dicValue;
}

internal abstract class PrimitiveToken
{
    protected PrimitiveToken() { }

    internal PrimitiveToken(PrimitiveTokenDataSet dataSet)
    {
        _codeType = dataSet._dicValue._type;
        _codeExtraFlags = dataSet._dicValue._extra;
        _dicValueName = dataSet._dicValue._name;
        OperatorPriority = dataSet._dicValue._operatorPriority;
        TokenOffset = dataSet._tokenOffset;
        _type = dataSet._type;
        _flag = dataSet._flag;
        _value = dataSet._value;
        _name = dataSet._name;
        Style = dataSet._parent?.Style ?? SourceStyle.Default;
        _data = dataSet._parent;
    }

    private readonly AxData? _data;

    // The releases that could have written the AX, which tell which hspdef.as macros the
    // compiler had. Read when the token renders, after the whole AX is known.
    //
    // AX を書いた可能性のあるリリース。コンパイラにどの hspdef.as マクロがあったかを示す。
    // AX 全体が分かった後、トークンを書く時に読む。
    internal ReleaseProfile Release => _data?.Release ?? ReleaseProfile.Unknown;

    internal SourceStyle Style { get; } = SourceStyle.Default;

    protected string _dicValueName = "null";
    protected readonly int _type;
    protected readonly HspCodeType _codeType;

    protected readonly int _flag;
    protected readonly int _value;

    internal int Value => _value;

    // True for the code hspcmp wrote with this type and value, before any dictionary naming.
    //
    // hspcmp がこの型と値で書いたコードなら真。辞書による名前付けの前の値で比べる。
    internal bool IsCode(int type, int value) => _type == type && _value == value;

    private readonly HspCodeExtraOptions _codeExtraFlags;
    private string? _name;

    internal bool HasGhostLabel =>
        IsLineHead
        && (_codeExtraFlags & HspCodeExtraOptions.HasGhostLabel)
            == HspCodeExtraOptions.HasGhostLabel;

    internal HspCodeType CodeType => _codeType;
    internal HspCodeExtraOptions CodeExtraFlags => _codeExtraFlags;

    internal int OperatorPriority =>
        _codeType != HspCodeType.Operator
            ? throw new InvalidOperationException(Strings.OperatorPriorityOnNonOperator)
            : field;

    internal bool HasLongTypeValue =>
        ((_flag & HspTokenFlag.LongTypeValue) == HspTokenFlag.LongTypeValue);

    internal bool IsParamHead => ((_flag & HspTokenFlag.ParamHead) == HspTokenFlag.ParamHead);
    internal bool IsLineHead => ((_flag & HspTokenFlag.LineHead) == HspTokenFlag.LineHead);

    internal string? Name => _name;

    internal int TokenOffset { get; }

    internal void SetName()
    {
        if (
            _codeType
            is HspCodeType.Label
                or HspCodeType.IntegerValue
                or HspCodeType.Param
                or HspCodeType.Variable
        )
        {
            _name = _dicValueName + _value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public override string ToString() => _name ?? string.Empty;

    internal virtual string DefaultName
    {
        get
        {
            var builder = new StringBuilder();
            builder.Append("/*");
            builder.Append(_type.ToString("X02", CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append(_flag.ToString("X02", CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append(
                HasLongTypeValue
                    ? _value.ToString("X08", CultureInfo.InvariantCulture)
                    : _value.ToString("X04", CultureInfo.InvariantCulture)
            );

            builder.Append("*/");
            return builder.ToString();
        }
    }
}
