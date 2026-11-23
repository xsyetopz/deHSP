using HspDecompiler.Core.Ax3.Data.Primitive;

namespace HspDecompiler.Core.Ax3.Data.Token;

internal sealed class LiteralToken : OperandToken
{
    internal LiteralToken(LiteralPrimitive token)
    {
        _token = token;
    }

    private readonly LiteralPrimitive? _token;
    internal bool IsNegativeNumber => _token != null && _token.IsNegativeNumber;

    internal bool IsMinusOne => _token!.IsMinusOne;

    internal override int TokenOffset => _token == null ? -1 : _token.TokenOffset;

    public override string ToString() =>
        (_token!.CodeType == HspCodeType.Symbol) && (_token.ToString() == "?")
            ? ""
            : _token.ToString();

    internal override int Priority =>
        IsNegativeNumber && _token!.Style.ParenthesizeNegatives ? -1 : 100;

    // An integer that is not a label or a symbol, or null.
    //
    // ラベルや記号ではない整数。それ以外は null。
    internal int? Integer => _token is IntegerPrimitive integer ? integer.Value : null;

    internal void WriteInHex()
    {
        if (_token is IntegerPrimitive integer)
        {
            integer.Hex = true;
        }
    }

    internal override void CheckLabel()
    {
        var label = _token as LabelPrimitive;
        label?.LabelIsUsed();
    }
}
