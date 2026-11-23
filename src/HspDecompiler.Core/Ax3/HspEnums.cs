using System;

namespace HspDecompiler.Core.Ax3;

/// <summary>
/// <para xml:lang="en">
/// What a code word in an HSP 3 object file stands for. Values up to 0x12 are the type codes
/// hspcmp writes (TYPE_* in hsp3struct.h); 0xFE, 0xFF and 0x20 to 0x23 are dictionary groups the
/// decompiler adds for operators, symbols and statements that need their own layout.
/// </para>
/// <para xml:lang="ja">
/// HSP 3 オブジェクトファイルのコードが表すもの。0x12 までの値は hspcmp が書き出す型コード
/// (hsp3struct.h の TYPE_*)。0xFE・0xFF・0x20〜0x23 は、演算子・記号・独自の書式が必要な文の
/// ためにデコンパイラが加えた辞書上の分類。
/// </para>
/// </summary>
public enum HspCodeType
{
    /// <summary><para xml:lang="en">No type.</para><para xml:lang="ja">型なし。</para></summary>
    None = 0x00,

    /// <summary><para xml:lang="en">Operator.</para><para xml:lang="ja">演算子。</para></summary>
    Operator = 0xFF,

    /// <summary><para xml:lang="en">Symbol such as a bracket or comma.</para><para xml:lang="ja">括弧やカンマなどの記号。</para></summary>
    Symbol = 0xFE,

    /// <summary><para xml:lang="en">Variable (TYPE_VAR).</para><para xml:lang="ja">変数(TYPE_VAR)。</para></summary>
    Variable = 0x01,

    /// <summary><para xml:lang="en">String literal (TYPE_STRING).</para><para xml:lang="ja">文字列リテラル(TYPE_STRING)。</para></summary>
    StringValue = 0x02,

    /// <summary><para xml:lang="en">Real-number literal (TYPE_DNUM).</para><para xml:lang="ja">実数リテラル(TYPE_DNUM)。</para></summary>
    DoubleValue = 0x03,

    /// <summary><para xml:lang="en">Integer literal (TYPE_INUM).</para><para xml:lang="ja">整数リテラル(TYPE_INUM)。</para></summary>
    IntegerValue = 0x04,

    /// <summary><para xml:lang="en">Module parameter (TYPE_STRUCT).</para><para xml:lang="ja">モジュールの引数(TYPE_STRUCT)。</para></summary>
    Param = 0x05,

    /// <summary><para xml:lang="en">Label (TYPE_LABEL).</para><para xml:lang="ja">ラベル(TYPE_LABEL)。</para></summary>
    Label = 0x07,

    /// <summary>
    /// <para xml:lang="en">Built-in command, function or system variable (TYPE_INTCMD and the groups after it).</para>
    /// <para xml:lang="ja">組み込みの命令・関数・システム変数(TYPE_INTCMD とそれに続く分類)。</para>
    /// </summary>
    HspFunction = 0x08,

    /// <summary><para xml:lang="en">if or else (TYPE_CMPCMD).</para><para xml:lang="ja">if または else(TYPE_CMPCMD)。</para></summary>
    IfStatement = 0x0B,

    /// <summary><para xml:lang="en">User-defined command or function (TYPE_MODCMD).</para><para xml:lang="ja">ユーザー定義の命令・関数(TYPE_MODCMD)。</para></summary>
    UserFunction = 0x0C,

    /// <summary><para xml:lang="en">DLL import (TYPE_DLLFUNC).</para><para xml:lang="ja">DLL のインポート(TYPE_DLLFUNC)。</para></summary>
    DllFunction = 0x10,

    /// <summary><para xml:lang="en">COM method or DLL control command (TYPE_DLLCTRL).</para><para xml:lang="ja">COM のメソッドまたは DLL 制御命令(TYPE_DLLCTRL)。</para></summary>
    ComFunction = 0x11,

    /// <summary><para xml:lang="en">Plug-in command (TYPE_USERDEF).</para><para xml:lang="ja">プラグインの命令(TYPE_USERDEF)。</para></summary>
    PlugInFunction = 0x12,

    /// <summary><para xml:lang="en">onexit, onerror and the other event handlers.</para><para xml:lang="ja">onexit・onerror などのイベント設定命令。</para></summary>
    OnEventStatement = 0x20,

    /// <summary><para xml:lang="en">on n goto/gosub.</para><para xml:lang="ja">on n goto/gosub。</para></summary>
    OnStatement = 0x21,

    /// <summary><para xml:lang="en">else.</para><para xml:lang="ja">else。</para></summary>
    ElseStatement = 0x22,

    /// <summary><para xml:lang="en">mcall.</para><para xml:lang="ja">mcall。</para></summary>
    McallStatement = 0x23,
}

/// <summary>
/// <para xml:lang="en">
/// Layout hints the dictionary attaches to a code. "Ghost" marks a label or goto that hspcmp
/// generates for repeat, foreach, break, continue and stop, which the output leaves out.
/// </para>
/// <para xml:lang="ja">
/// 辞書がコードに付ける書式の指定。「ゴースト」は hspcmp が repeat・foreach・break・continue・stop のために生成する
/// ラベルや goto を表し、出力には含めない。
/// </para>
/// </summary>
[Flags]
public enum HspCodeExtraOptions
{
    /// <summary><para xml:lang="en">No hints.</para><para xml:lang="ja">指定なし。</para></summary>
    None = 0x00,

    /// <summary>
    /// <para xml:lang="en">A 16-bit jump offset follows the code at the start of a line.</para>
    /// <para xml:lang="ja">行頭のコードの後に 16 ビットのジャンプオフセットが続く。</para>
    /// </summary>
    HasExtraInt16 = 0x0001,

    /// <summary>
    /// <para xml:lang="en">A generated label argument follows and is left out.</para>
    /// <para xml:lang="ja">生成されたラベルの引数が後に続き、出力しない。</para>
    /// </summary>
    HasGhostLabel = 0x0002,

    /// <summary>
    /// <para xml:lang="en">The command is followed by a generated goto.</para>
    /// <para xml:lang="ja">命令の後に生成された goto が続く。</para>
    /// </summary>
    HasGhostGoto = 0x0004,

    /// <summary><para xml:lang="en">Indents the lines that follow.</para><para xml:lang="ja">後続の行を字下げする。</para></summary>
    AddTab = 0x0008,

    /// <summary><para xml:lang="en">Ends the indentation, starting with this line.</para><para xml:lang="ja">この行から字下げを戻す。</para></summary>
    RemoveTab = 0x0010,

    /// <summary>
    /// <para xml:lang="en">The command itself is generated and left out.</para>
    /// <para xml:lang="ja">命令自体が生成されたもので、出力しない。</para>
    /// </summary>
    IsGhost = 0x0020,

    /// <summary><para xml:lang="en">Opens a bracket.</para><para xml:lang="ja">括弧を開く。</para></summary>
    BracketStart = 0x0040,

    /// <summary><para xml:lang="en">Closes a bracket.</para><para xml:lang="ja">括弧を閉じる。</para></summary>
    BracketEnd = 0x0080,

    /// <summary>
    /// <para xml:lang="en">goto or gosub that an on or event statement takes as its jump.</para>
    /// <para xml:lang="ja">on 文やイベント設定命令がジャンプとして受け取る goto または gosub。</para>
    /// </summary>
    GotoFunction = 0x0100,
}
