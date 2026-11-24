using System.Collections.Immutable;

namespace HspDecompiler.Core.Ax1.Data;

// One statement, the tokens before the 0x80 that ends it. ASC writes else as a statement that
// starts with 0x80, so an Else holds only the label offset of its slot. End is the offset past
// the closing 0x80, where the next statement starts.
//
// 1 つの文。文を終える 0x80 より前のトークン。ASC は else を 0x80 で始まる文として書くため、
// Else はスロットのラベルのオフセットだけを持つ。End は閉じる 0x80 の次のオフセットで、次の
// 文はそこから始まる。
internal sealed record Ax1Statement(
    int Offset,
    int End,
    ImmutableArray<Ax1Token> Tokens,
    bool IsElse
)
{
    internal bool StartsWith(int value) => !Tokens.IsEmpty && Tokens[0].IsByte(value);
}
