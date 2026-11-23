namespace HspDecompiler.Core.Ax3.Data.Primitive;

// The operator codes hspcmp writes with token type 0 (CALCCODE_* in the HSP 3 sources). The
// spelling comes from the dictionary, which a caller may replace, so the decoder decides by code.
//
// hspcmp がトークン型 0 で書く演算子コード(HSP 3 のソースの CALCCODE_*)。綴りは呼び出し側が
// 差し替えられる辞書から来るため、デコーダーはコードで判断する。
internal enum CalcCode
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    And,
    Or,
    Xor,
    Equal,
    NotEqual,
    Greater,
    Less,
    GreaterOrEqual,
    LessOrEqual,
    ShiftRight,
    ShiftLeft,
}

internal static class CalcCodes
{
    internal static bool IsComparison(this CalcCode code) =>
        code is >= CalcCode.Equal and <= CalcCode.LessOrEqual;
}
