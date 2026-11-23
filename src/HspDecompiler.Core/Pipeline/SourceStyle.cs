namespace HspDecompiler.Core.Pipeline;

/// <summary>
/// <para xml:lang="en">
/// How the written source spells constructs that hspcmp compiles to the same bytes. The AX
/// keeps none of these choices, so each one only changes how the source reads; every setting
/// recompiles to identical code.
/// </para>
/// <para xml:lang="ja">
/// hspcmp が同じバイト列にコンパイルする構文を、書き出すソースでどう綴るか。AX はこれらの
/// 選択を残さないため、どれもソースの読み方を変えるだけで、どの設定でも同一のコードに
/// 再コンパイルされる。
/// </para>
/// </summary>
public sealed record SourceStyle
{
    /// <summary>
    /// <para xml:lang="en">Parentheses around the condition of an if. Default: none.</para>
    /// <para xml:lang="ja">if の条件を囲む括弧。既定はなし。</para>
    /// </summary>
    public IfParentheses IfParentheses { get; init; } = IfParentheses.None;

    /// <summary>
    /// <para xml:lang="en">
    /// How an if whose whole block shares its source line is written: <c>if x : y</c> or
    /// <c>if x { y }</c>. Only a debug build records source lines. Default: colon.
    /// </para>
    /// <para xml:lang="ja">
    /// ブロック全体が同じソース行にある if の書き方。<c>if x : y</c> か <c>if x { y }</c>。
    /// ソース行を記録するのはデバッグビルドだけである。既定はコロン。
    /// </para>
    /// </summary>
    public OneLineIf OneLineIf { get; init; } = OneLineIf.Colon;

    /// <summary>
    /// <para xml:lang="en">
    /// Whether a negative literal next to an operator gets parentheses: <c>a - (-1)</c> rather
    /// than <c>a - -1</c>. Default: false.
    /// </para>
    /// <para xml:lang="ja">
    /// 演算子の隣の負のリテラルを括弧で囲むか。<c>a - -1</c> ではなく <c>a - (-1)</c>。
    /// 既定は false。
    /// </para>
    /// </summary>
    public bool ParenthesizeNegatives { get; init; }

    /// <summary>
    /// <para xml:lang="en">
    /// How &amp; and | between two conditions are written. Bitwise uses on other operands keep
    /// &amp; and |. Default: symbols.
    /// </para>
    /// <para xml:lang="ja">
    /// 二つの条件の間の &amp; と | の書き方。それ以外のビット演算は &amp; と | のまま。
    /// 既定は記号。
    /// </para>
    /// </summary>
    public LogicalOperators LogicalOperators { get; init; } = LogicalOperators.Symbols;

    /// <summary>
    /// <para xml:lang="en">
    /// Whether binary operators go without spaces: <c>strlen(s)-1</c> rather than
    /// <c>strlen(s) - 1</c>. A negative right operand keeps one space before it. Default: false.
    /// </para>
    /// <para xml:lang="ja">
    /// 二項演算子の前後の空白を省くか。<c>strlen(s) - 1</c> ではなく <c>strlen(s)-1</c>。
    /// 負の右オペランドの前には空白を一つ残す。既定は false。
    /// </para>
    /// </summary>
    public bool CompactOperators { get; init; }

    /// <summary>
    /// <para xml:lang="en">
    /// Whether calls that a hspdef.as macro expands to are written as the macro:
    /// <c>dir_exe</c> rather than <c>dirinfo(1)</c>. Default: true.
    /// </para>
    /// <para xml:lang="ja">
    /// hspdef.as のマクロが展開される呼び出しをマクロとして書くか。<c>dirinfo(1)</c> ではなく
    /// <c>dir_exe</c>。既定は true。
    /// </para>
    /// </summary>
    public bool StandardMacros { get; init; } = true;

    /// <summary>
    /// <para xml:lang="en">Which integer literals are written in hexadecimal. Default: none.</para>
    /// <para xml:lang="ja">16 進数で書く整数リテラル。既定はなし。</para>
    /// </summary>
    public HexLiterals HexLiterals { get; init; } = HexLiterals.None;

    internal static SourceStyle Default { get; } = new();
}

/// <summary>
/// <para xml:lang="en">Parentheses around the condition of an if.</para>
/// <para xml:lang="ja">if の条件を囲む括弧。</para>
/// </summary>
public enum IfParentheses
{
    /// <summary>
    /// <para xml:lang="en"><c>if a == 1 {</c></para>
    /// <para xml:lang="ja"><c>if a == 1 {</c></para>
    /// </summary>
    None,

    /// <summary>
    /// <para xml:lang="en"><c>if (a == 1) {</c></para>
    /// <para xml:lang="ja"><c>if (a == 1) {</c></para>
    /// </summary>
    Tight,

    /// <summary>
    /// <para xml:lang="en"><c>if ( a == 1 ) {</c></para>
    /// <para xml:lang="ja"><c>if ( a == 1 ) {</c></para>
    /// </summary>
    Spaced,
}

/// <summary>
/// <para xml:lang="en">How an if whose block fits on its source line is written.</para>
/// <para xml:lang="ja">ブロックがソース行に収まる if の書き方。</para>
/// </summary>
public enum OneLineIf
{
    /// <summary>
    /// <para xml:lang="en"><c>if a : mes "x" : else : mes "y"</c></para>
    /// <para xml:lang="ja"><c>if a : mes "x" : else : mes "y"</c></para>
    /// </summary>
    Colon,

    /// <summary>
    /// <para xml:lang="en"><c>if a { mes "x" } else { mes "y" }</c></para>
    /// <para xml:lang="ja"><c>if a { mes "x" } else { mes "y" }</c></para>
    /// </summary>
    Braces,
}

/// <summary>
/// <para xml:lang="en">How &amp; and | between two conditions are written.</para>
/// <para xml:lang="ja">二つの条件の間の &amp; と | の書き方。</para>
/// </summary>
public enum LogicalOperators
{
    /// <summary>
    /// <para xml:lang="en"><c>a == 1 &amp; b == 2</c></para>
    /// <para xml:lang="ja"><c>a == 1 &amp; b == 2</c></para>
    /// </summary>
    Symbols,

    /// <summary>
    /// <para xml:lang="en"><c>(a == 1) &amp;&amp; (b == 2)</c></para>
    /// <para xml:lang="ja"><c>(a == 1) &amp;&amp; (b == 2)</c></para>
    /// </summary>
    Doubled,

    /// <summary>
    /// <para xml:lang="en"><c>(a == 1) and (b == 2)</c>, the hspdef.as macros.</para>
    /// <para xml:lang="ja"><c>(a == 1) and (b == 2)</c>。hspdef.as のマクロ。</para>
    /// </summary>
    Words,
}

/// <summary>
/// <para xml:lang="en">Which integer literals are written in hexadecimal.</para>
/// <para xml:lang="ja">16 進数で書く整数リテラル。</para>
/// </summary>
public enum HexLiterals
{
    /// <summary>
    /// <para xml:lang="en">Every integer in decimal.</para>
    /// <para xml:lang="ja">すべての整数を 10 進数で書く。</para>
    /// </summary>
    None,

    /// <summary>
    /// <para xml:lang="en">
    /// The value argument of poke, wpoke and lpoke when it is 128 or more, the bytes past ASCII:
    /// <c>poke buf, 0, 0xEF</c>.
    /// </para>
    /// <para xml:lang="ja">
    /// poke・wpoke・lpoke の値の引数が 128 以上(ASCII より後のバイト)の時:
    /// <c>poke buf, 0, 0xEF</c>。
    /// </para>
    /// </summary>
    Poke,

    /// <summary>
    /// <para xml:lang="en">Every non-negative integer of 10 or more: <c>x = 0xFF</c>.</para>
    /// <para xml:lang="ja">10 以上のすべての非負の整数:<c>x = 0xFF</c>。</para>
    /// </summary>
    All,
}
