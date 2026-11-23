using System.Text;
using HspDecompiler.Core.Ax3.Data.PP;

namespace HspDecompiler.Core.Ax3.Data.Primitive;

internal abstract class VariablePrimitive : OperandPrimitive
{
    protected VariablePrimitive() { }

    internal VariablePrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet) { }
}

internal sealed class GlobalVariablePrimitive : VariablePrimitive
{
    // The name is looked up at output time, because its encoding is known only after the lexer
    // has seen every script string.
    //
    // 名前のエンコーディングは字句解析が全スクリプト文字列を見た後でしか決まらないため、
    // 出力時に名前を引く。
    private readonly AxData _data;

    internal GlobalVariablePrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet)
    {
        _data = dataSet._parent!;
    }

    internal override string Render(RenderContext context)
    {
        string? varName = _data.GetVariableName(Value);
        if (varName != null)
        {
            return context.Variable(varName);
        }

        var bld = new StringBuilder("var");
        bld.Append('_');
        bld.Append(Value);
        return bld.ToString();
    }
}

internal sealed class ParameterPrimitive : VariablePrimitive
{
    internal ParameterPrimitive(PrimitiveTokenDataSet dataSet)
        : base(dataSet)
    {
        _param = dataSet._parent!.GetParam(Value);
        _param?.ParamNameIsUsed = true;
        if (_param is { IsStructTag: true, Module: { } module })
        {
            module.MarkUsedAt(TokenOffset);
        }
    }

    private readonly Param? _param;

    // hspcmp codes thismod as a parameter reference with index -1 (hspcmd.cpp).
    //
    // hspcmp は thismod を番号 -1 の引数参照として書く(hspcmd.cpp)。
    private const int ThisModIndex = -1;

    internal override string Render(RenderContext context) =>
        _param != null ? _param.ParamName
        : Value == ThisModIndex ? "thismod"
        : DefaultName;
}
