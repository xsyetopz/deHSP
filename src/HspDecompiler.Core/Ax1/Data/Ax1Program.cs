using System.Collections.Immutable;

namespace HspDecompiler.Core.Ax1.Data;

// An "Aspo" image as ASC wrote it. The 32-byte header holds 16 little-endian words: the magic,
// zero, the code size with the final 0x8f, the next global and the next local variable index.
// The code starts at 0x20 and the NUL-terminated strings follow it.
//
// ASC が書いた "Aspo" イメージ。32 バイトのヘッダーは 16 個のリトルエンディアンのワードを持つ。
// マジック、0、最後の 0x8f を含むコードの大きさ、次のグローバル変数と次のローカル変数の番号で
// ある。コードは 0x20 から始まり、NUL で終わる文字列がその後に続く。
internal sealed record Ax1Program(
    int NextGlobal,
    int NextLocal,
    ImmutableArray<Ax1Statement> Statements,
    ImmutableArray<Ax1Token> Lines,
    int EndOffset,
    byte[] Strings
)
{
    // ASC turns label indices into offsets only up to its first 0x8f token, and only when every
    // label is defined; FirstEnd is that token's offset.
    //
    // ASC がラベルの番号をオフセットに変えるのは最初の 0x8f のトークンまでで、すべてのラベルが
    // 定義された時だけである。FirstEnd はそのトークンのオフセット。
    internal int FirstEnd { get; init; }

    internal bool Unresolved { get; init; }

    internal bool IsDebug => !Lines.IsEmpty;
}
