using System.Collections.Generic;

namespace HspDecompiler.Core.Ax3.Data.Layout;

// One output line of the debug layout. Code, an #include and a #module are anchors: they stay on
// the line the debug segment gives them. A closing brace, a #global and a header directive go as
// early as the order allows, every other line just before the anchor that follows it.
//
// デバッグの配置の出力 1 行。コード・#include・#module はアンカーであり、デバッグセグメントが
// 示す行から動かない。閉じ括弧・#global・ヘッダーの指定は順序が許す限り前に、それ以外の行は
// 後に続くアンカーの直前に置く。
internal sealed class LayoutItem
{
    internal LayoutItem(LineKind kind, string text, int tabs, int file, int target)
    {
        Kind = kind;
        Text = text;
        Tabs = tabs;
        File = file;
        Target = target;
    }

    internal LineKind Kind { get; }

    internal string Text { get; }

    internal int Tabs { get; }

    internal int File { get; set; }

    // The 1-based source line, or 0 for a line the debug segment does not place.
    //
    // 1 始まりのソース行。デバッグセグメントが位置を示さない行では 0。
    internal int Target { get; }

    // The code offset a #global stands at, or -1 for any other line.
    //
    // #global が立つコード位置。それ以外の行では -1。
    internal int CodeOffset { get; init; } = -1;

    // The if or else block this line opens or closes, numbered in source order, or -1. An else
    // that follows the close of an if continues that block's chain.
    //
    // この行が開く・閉じる if または else のブロックの番号(ソース順)。無ければ -1。if の閉じ
    // 括弧の直後の else は、そのブロックの連なりを続ける。
    internal int Opens { get; set; } = -1;

    internal int Closes { get; set; } = -1;

    internal int Continues { get; set; } = -1;

    // True for an else statement, which follows the closing brace before it on that line.
    //
    // else 文なら true。直前の閉じ括弧と同じ行に続ける。
    internal bool IsElse { get; init; }

    // The items a joined line was built from, such as a label and the statement it names.
    //
    // つないだ行の元になった項目。ラベルとそれが指す文など。
    internal IReadOnlyList<LayoutItem>? Parts { get; init; }

    internal bool IsAnchor => Kind is LineKind.Code or LineKind.Include or LineKind.ModuleStart;

    internal bool IsEarly => Kind is LineKind.Close or LineKind.ModuleEnd or LineKind.Header;
}
