using HspDecompiler.Core.Ax3.Data.Line;
using HspDecompiler.Core.Ax3.Data.PP;

namespace HspDecompiler.Core.Ax3.Data.Layout;

// What an output line is to both layouts: the debug layout that puts lines back on recorded
// source lines and the marked layout of a normal build. Code and a closing brace may follow code,
// a brace or a label on its line ("*l : mes", "} else {"). Every directive, including a header
// (#runtime), #module, #global and an #include, takes a line of its own and lets nothing follow
// it. A blank line carries nothing and is the first to drop when a gap is short.
//
// 両方の配置にとって出力行が何であるか。記録されたソース行へ行を戻すデバッグの配置と、通常
// ビルドの印による配置である。コードと閉じ括弧は、コード・括弧・ラベルの後ろに同じ行で
// 続けられる("*l : mes"、"} else {")。ヘッダー(#runtime)・#module・#global・#include を
// 含むすべての指示文は自分の行を占め、後ろに何も続けさせない。空行は何も持たず、間が
// 足りない時に最初に落ちる。
internal enum LineKind
{
    Code,
    Close,
    Label,
    Blank,
    Directive,
    Header,
    ModuleStart,
    ModuleEnd,
    Include,
}

internal static class LineKinds
{
    internal static bool TakesJoin(this LineKind kind) => kind is LineKind.Code or LineKind.Close;

    internal static bool LetsJoin(this LineKind kind) =>
        kind is LineKind.Code or LineKind.Close or LineKind.Label;

    // The kind of a decoded line. An #include has no line of its own in the decoded code; the
    // layout that writes one names its kind.
    //
    // デコードした行の種類。#include はデコードしたコードに自分の行を持たないため、それを書く
    // 配置が種類を決める。
    internal static LineKind Of(LogicalLine line) =>
        line switch
        {
            ScopeEnd => LineKind.Close,
            ModuleStart => LineKind.ModuleStart,
            EndOfModule => LineKind.ModuleEnd,
            PreprocessorDeclaration { Declared: Runtime } => LineKind.Header,
            PreprocessorDeclaration { Declared: Label { Function: null } } => LineKind.Label,
            CommentLine { IsBlank: true } => LineKind.Blank,
            PreprocessorDeclaration or CommentLine or UnknownLine => LineKind.Directive,
            _ => LineKind.Code,
        };
}
