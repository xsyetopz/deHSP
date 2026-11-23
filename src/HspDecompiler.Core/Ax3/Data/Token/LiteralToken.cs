using HspDecompiler.Core.Ax3.Data.PP;
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

    internal override string Render(RenderContext context) =>
        _token!.Render(context) is var text && _token.CodeType == HspCodeType.Symbol && text == "?"
            ? ""
            : text;

    internal override int Priority(RenderContext context) =>
        IsNegativeNumber && context.Style.ParenthesizeNegatives ? -1 : 100;

    // An integer that is not a label or a symbol, or null.
    //
    // ラベルや記号ではない整数。それ以外は null。
    internal Label? Label => (_token as LabelPrimitive)?.Label;

    internal int? Integer => _token is IntegerPrimitive integer ? integer.Value : null;

    internal void MarkPokeByte()
    {
        if (_token is IntegerPrimitive integer)
        {
            integer.PokeByte = true;
        }
    }

    internal override void CheckLabel()
    {
        var label = _token as LabelPrimitive;
        label?.LabelIsUsed();
    }
}
