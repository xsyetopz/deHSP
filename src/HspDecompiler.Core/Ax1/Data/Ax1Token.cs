namespace HspDecompiler.Core.Ax1.Data;

// The token kinds of ASC's code. A first byte below 0x80 starts a two-byte big-endian number,
// 0x80 to 0xef are single-byte commands and operators, and 0xf0 to 0xff prefix a 16-bit
// little-endian value, except 0xf1, which carries one byte.
//
// ASC のコードのトークンの種類。先頭バイトが 0x80 未満なら 2 バイトのビッグエンディアンの
// 数値、0x80〜0xef は 1 バイトの命令と演算子、0xf0〜0xff は 16 ビットのリトルエンディアンの値の
// 前置きである。ただし 0xf1 は 1 バイトを持つ。
internal enum Ax1TokenKind
{
    Number,
    WideNumber,
    Byte,
    Variable,
    Local,
    String,
    LabelOffset,
    LabelIndex,
    If,
    Line,
    Unknown,
}

internal readonly record struct Ax1Token(Ax1TokenKind Kind, int Offset, int Size, int Value)
{
    internal int End => Offset + Size;

    internal bool IsByte(int value) => Kind == Ax1TokenKind.Byte && Value == value;
}
