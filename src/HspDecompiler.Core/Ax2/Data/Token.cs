using System;
using System.Globalization;
using System.Text;
using HspDecompiler.Core.Encoding;
using HspDecompiler.Core.Releases;

namespace HspDecompiler.Core.Ax2.Data;

internal class Token
{
    private readonly AxData _data;

    private Token(AxData data)
    {
        _data = data;
    }

    private int _fType;
    private int _fValue;
    private int _id;
    private int _size;
    internal bool _isLineend;
    internal int _hiddenLabelIndex = -1;
    internal int _ifJumpTo = -1;

    internal static Token? GetToken(AxData data, int offset)
    {
        if ((offset < 0) || ((offset + 1) >= data.TokenData.Length))
        {
            return null;
        }

        var ret = new Token(data) { _id = offset / 2, _fValue = data.TokenData[offset] };
        offset++;
        ret._fType = data.TokenData[offset];
        offset++;
        ret._size = 2;
        if (data.Release.TokenLayout == Ax2TokenLayout.ByteValue)
        {
            ret.ReadByteValueLayout(offset);
            return ret;
        }

        if ((ret._fType & Ax2TokenType.ExtendedValueBit) != 0)
        {
            ret._fType ^= Ax2TokenType.ExtendedValueBit;
            ret._fValue += Ax2TokenType.ExtendedValueCarry;
        }
        if ((ret.Type == Ax2TokenType.IfElse) && ((ret.Value == 0) || (ret.Value == 1)))
        {
            ret._size += 2;
            ret._ifJumpTo = BitConverter.ToInt16(data.TokenData, offset);
            offset += 2;
            ret._ifJumpTo += ret._id + 2;
        }
        if (((ret._fType & Ax2TokenType.LongValueFlag) != 0))
        {
            ret._fType ^= Ax2TokenType.LongValueFlag;
            ret._size += 4;
            ret._fValue = BitConverter.ToInt32(data.TokenData, offset);
        }
        return ret;
    }

    // hspcmp 2.5 keeps the type and flag bits one place lower than 2.55 and follows a larger
    // value with 16 bits (0x80) or 32 bits (0xC0); shifting the type byte up gives the 2.55
    // layout the rest of the decoder reads.
    //
    // hspcmp 2.5 は型と印のビットを 2.55 より 1 つ下に置き、より大きな値には 16 ビット(0x80)か
    // 32 ビット(0xC0)を続ける。型のバイトを上へずらすと、デコーダーの残りが読む 2.55 の形になる。
    private void ReadByteValueLayout(int offset)
    {
        int extension = _fType & 0xC0;
        _fType = (_fType & 0x3F) << 1;
        if ((Type == Ax2TokenType.IfElse) && (Value is 0 or 1))
        {
            _size += 2;
            _ifJumpTo = BitConverter.ToInt16(_data.TokenData, offset) + _id + 2;
        }
        else if (extension == 0xC0)
        {
            _size += 4;
            _fValue = BitConverter.ToInt32(_data.TokenData, offset);
        }
        else if (extension == 0x80)
        {
            _size += 2;
            _fValue = BitConverter.ToUInt16(_data.TokenData, offset);
        }
    }

    internal int Size => _size;

    #region properties
    private int Type => _fType & Ax2TokenType.TypeMask;

    private int Value => _fValue;

    internal bool NextIsUnenableLabel =>
        (Type == Ax2TokenType.FlowControl)
        && (
            (_fValue == 0x03)
            || (_fValue == 0x2b)
            || ((_fValue == 0x11) && _data.Release.LabelsRepeat)
        );

    internal bool IsLinehead => (_fType & Ax2TokenType.LineHeadFlag) != 0;

    internal bool IsArg => (_fType & Ax2TokenType.ArgFlag) != 0;

    internal bool TabPlus
    {
        get
        {
            if (Type == Ax2TokenType.FlowControl)
            {
                if (Value == 0x11)
                {
                    return true;
                }
            }
            if (Type == Ax2TokenType.IfElse)
            {
                if (Value is 0 or 1)
                {
                    return true;
                }
            }
            return false;
        }
    }

    internal bool TabMinus
    {
        get
        {
            if (Type == Ax2TokenType.FlowControl)
            {
                if (Value == 0x12)
                {
                    return true;
                }
            }
            return false;
        }
    }

    internal int Id => _id;

    internal int IfJumpId => (Type == Ax2TokenType.IfElse) && (Value == 1) ? _id + 2 : _id;

    internal bool IsEnd => (Type == Ax2TokenType.EndProgram) && (Value == 0);

    internal bool IsEmpty => (_fType == 0) && (_fValue == 0);

    internal int LabelIndex =>
        IsLinehead ? -1
        : Type == Ax2TokenType.Label ? Value
        : -1;

    // hspcmp from 2.55 sets bit 0x02 on the label it appends to repeat, break and continue, so
    // that bit does not mark a line head there.
    //
    // 2.55 以降の hspcmp は repeat・break・continue に付けるラベルに 0x02 ビットを立てるため、
    // そこではこのビットは行頭を表さない。
    internal int OwnedLabelIndex => Type == Ax2TokenType.Label ? Value : -1;

    internal int StringOffset => Type == Ax2TokenType.String ? Value : -1;

    internal bool IsGoto => (Type == Ax2TokenType.FlowControl) && (Value == 0);

    internal int VariableIndex => Type == Ax2TokenType.Variable ? Value : -1;

    internal int FuncIndex => Type == Ax2TokenType.UserFunction ? Value - 0x10 : -1;
    #endregion

    #region properties for decompile
    internal string GetString() =>
        Type switch
        {
            Ax2TokenType.Operator => OperatorText(Value),
            Ax2TokenType.Integer => IntegerText(Value),
            Ax2TokenType.String => "\"" + Escape(_data.GetString(Value)) + "\"",
            Ax2TokenType.Label => "label_" + Value.ToString(CultureInfo.InvariantCulture),
            Ax2TokenType.Variable => "var_" + Value.ToString(CultureInfo.InvariantCulture),
            _ => NamedText() ?? RawText,
        };

    private string RawText =>
        Type.ToString("x2", CultureInfo.InvariantCulture)
        + Value.ToString("x2", CultureInfo.InvariantCulture);

    private string? NamedText() =>
        Type switch
        {
            Ax2TokenType.SystemVariable or Ax2TokenType.FlowControl or Ax2TokenType.GuiFunction =>
                BuiltinReference(_data.Keywords.NameOf(Type, Value, _data.Release)),
            Ax2TokenType.UserFunction => _data.GetFuncName(Value - 0x10) is { } name
                ? "func_" + name
                : null,
            Ax2TokenType.IfElse => Value switch
            {
                0 => "if",
                1 => "else",
                _ => null,
            },
            Ax2TokenType.Deffunc => _data.GetDeffuncName(Value - 0x10),
            Ax2TokenType.EndProgram when Value == 0 => "end",
            _ => null,
        };

    private static string OperatorText(int value) =>
        value switch
        {
            0x61 => "<=",
            0x62 => ">=",
            0x63 => "<<",
            0x64 => ">>",
            _ => ShiftJisHelper.Encoding.GetString([(byte)value]),
        };

    // hspcmp 2.x reads "-11" as a minus operator before 11, so a negative literal in the AX
    // came from hex source such as $fffffff5.
    //
    // hspcmp 2.x は "-11" を 11 の前のマイナス演算子として読むため、AX の負のリテラルは
    // $fffffff5 のような 16 進表記のソースから来ている。
    private static string IntegerText(int value) =>
        value < 0
            ? "$" + value.ToString("x8", CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);

    private string? BuiltinReference(string? name) =>
        name == null ? null
        : _data.IsShadowedBuiltin(name) ? name + "@hsp"
        : name;

    internal bool IsKnown => !GetString().Equals(RawText, StringComparison.Ordinal);

    // hspcmp 2.x turns "\n" into CR LF and "\r" into a lone CR, so a CR LF pair goes back to
    // "\n" and any other CR to "\r".
    //
    // hspcmp 2.x は "\n" を CR LF に、"\r" を単独の CR に変換する。そのため CR LF の組は
    // "\n" に、それ以外の CR は "\r" に戻す。
    private static string Escape(string str)
    {
        var escaped = new StringBuilder(str.Length);
        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
            switch (c)
            {
                case '\r' when (i + 1 < str.Length) && (str[i + 1] == '\n'):
                    break;
                case '\r':
                    escaped.Append("\\r");
                    break;
                case '\n':
                    escaped.Append("\\n");
                    break;
                case '\t':
                    escaped.Append("\\t");
                    break;
                case '"':
                    escaped.Append("\\\"");
                    break;
                case '\\':
                    escaped.Append("\\\\");
                    break;
                default:
                    escaped.Append(c);
                    break;
            }
        }

        return escaped.ToString();
    }
    #endregion
}
