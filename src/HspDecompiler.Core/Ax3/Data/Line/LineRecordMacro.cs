namespace HspDecompiler.Core.Ax3.Data.Line;

// A line that makes hspcmp 3.1 to 3.32 write a line record with no code. hspcmp writes "##N"
// after a line whose macro expansion breaks into more lines than the source took (token.cpp
// ExpandLine, "lineext != mline"), and those releases store each "##N" in a normal build too.
// The original source wrote such a record through a "%c" macro of its own, whose body the AX
// does not keep, so the output defines one macro that expands to a bare line break: each use
// writes the record of the line after it, and the definition stands on line 1. "%c" works only
// in a macro with parameters, and "global" lets a use inside a #module find it.
//
// hspcmp 3.1〜3.32 にコード無しで行の記録を書かせる行。hspcmp はマクロ展開がソースより多くの
// 行に分かれた行の後に "##N" を書き(token.cpp の ExpandLine、"lineext != mline")、これらの
// リリースは通常ビルドでも各 "##N" を記録する。元のソースは独自の "%c" マクロでその記録を
// 書いたが、AX はその本体を残さない。そこで出力は改行だけに展開されるマクロを 1 つ定義する。
// 各使用はその次の行の記録を書き、定義は 1 行目に置く。"%c" は引数を持つマクロでしか働かず、
// "global" は #module の中の使用からも見えるようにする。
internal sealed class LineRecordMacro : LogicalLine
{
    internal const string Name = "__line_record";

    private LineRecordMacro(bool definition)
    {
        IsDefinition = definition;
    }

    internal static LineRecordMacro Definition() => new(true);

    internal static LineRecordMacro Use() => new(false);

    internal bool IsDefinition { get; }

    internal override int TokenOffset => -1;

    internal override int TabCount => IsDefinition ? 0 : 1;

    public override string ToString() => IsDefinition ? $"#define global {Name}(%1) %c" : Name;
}
