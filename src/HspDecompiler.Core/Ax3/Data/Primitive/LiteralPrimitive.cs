using System.Globalization;
using HspDecompiler.Core.Ax3.Data.PP;
using HspDecompiler.Core.Pipeline;

namespace HspDecompiler.Core.Ax3.Data.Primitive;

internal abstract class LiteralPrimitive : OperandPrimitive
{
    protected LiteralPrimitive() { }

    internal virtual bool IsNegativeNumber => false;
    internal virtual bool IsMinusOne => false;

    internal LiteralPrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet) { }
}

internal sealed class LabelPrimitive : LiteralPrimitive
{
    internal LabelPrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet)
    {
        _label = dataSet._parent!.Labels[Value];
        _label.AddReference(TokenOffset);
    }

    private readonly Label? _label;

    internal Label? Label => _label;

    public override string ToString() => _label == null ? DefaultName : _label.LabelName;

    internal void LabelIsUsed()
    {
        if (_label == null)
        {
            return;
        }

        _label.Visible = true;
    }
}

internal sealed class IntegerPrimitive : LiteralPrimitive
{
    internal IntegerPrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet) { }

    internal override bool IsNegativeNumber => (Value < 0);
    internal override bool IsMinusOne => Value == -1;

    internal bool Hex { get; set; }

    // A negative value stays decimal: its hex spelling would be a 32-bit pattern such as
    // 0xFFFFFFFF that does not read as the number.
    //
    // 負の値は 10 進数のまま。16 進数で書くと 0xFFFFFFFF のような 32 ビットのパターンになり、
    // 数として読めない。
    public override string ToString() =>
        Value >= 0 && (Hex || (Style.HexLiterals == HexLiterals.All && Value >= 10))
            ? "0x" + Value.ToString("X", CultureInfo.InvariantCulture)
            : Value.ToString(CultureInfo.InvariantCulture);
}

internal sealed class DoublePrimitive : LiteralPrimitive
{
    internal DoublePrimitive(PrimitiveTokenDataSet dataSet, double d)
        : base(dataSet)
    {
        _d = d;
    }

    private readonly double _d;

    internal override bool IsNegativeNumber => (_d < 0.0);

    public override string ToString() => FormatLiteral(_d);

    // The shortest text that hspcmp parses back to the same bits. hspcmp reads a literal
    // through atof and treats it as a double only when it holds '.' or a lowercase 'e'
    // exponent (token.cpp), so integral values keep a ".0" suffix.
    //
    // hspcmp が同じビット値に読み戻せる最短の表記。hspcmp は atof で読み取り、'.' か
    // 小文字 'e' の指数を含む時だけ実数として扱う(token.cpp)ため、整数値には ".0" を付ける。
    internal static string FormatLiteral(double value)
    {
        string text = value.ToString("R", CultureInfo.InvariantCulture);
        return text.Contains('E') ? text.Replace('E', 'e')
            : text.Contains('.') ? text
            : text + ".0";
    }
}

internal sealed class StringPrimitive : LiteralPrimitive
{
    internal StringPrimitive(PrimitiveTokenDataSet dataSet, byte[] text)
        : base(dataSet)
    {
        _text = text;
        _strings = dataSet._parent!.ScriptStrings;
    }

    // The bytes stay raw until output, because the encoding is known only after the lexer has
    // seen every script string.
    //
    // エンコーディングは字句解析が全スクリプト文字列を見た後でしか決まらないため、出力時まで
    // バイト列のまま持つ。
    private readonly byte[] _text;
    private readonly ScriptStrings _strings;

    public override string ToString() => "\"" + _strings.Decode(_text) + "\"";
}

internal sealed class SymbolPrimitive : LiteralPrimitive
{
    internal SymbolPrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet) { }
}
