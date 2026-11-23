namespace HspDecompiler.Core.Ax3.Data.Layout;

// How an output line may move when the source layout is rebuilt. Code, an #include and a
// #module stay on the line the debug segment gives them. A closing brace, a #global and a
// header directive go as early as the order allows, every other line just before the code that
// follows it. A #module never joins other code, because a directive needs a line of its own.
//
// ソースの配置を組み直す時に出力行がどう動けるか。コード・#include・#module はデバッグ
// セグメントが示す行から動かない。閉じ括弧・#global・ヘッダーの指定は順序が許す限り前に、
// それ以外の行は後に続くコードの直前に置く。指示文は 1 行を占めるため、#module は他のコードと
// つながらない。
internal enum LayoutKind
{
    Code,
    Include,
    ModuleStart,
    Close,
    ModuleEnd,
    Header,
    Label,
    Blank,
    Other,
}

internal sealed class LayoutItem
{
    internal LayoutItem(LayoutKind kind, string text, int tabs, int file, int target)
    {
        Kind = kind;
        Text = text;
        Tabs = tabs;
        File = file;
        Target = target;
    }

    internal LayoutKind Kind { get; }

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

    internal bool IsAnchor =>
        Kind is LayoutKind.Code or LayoutKind.Include or LayoutKind.ModuleStart;

    internal bool IsEarly => Kind is LayoutKind.Close or LayoutKind.ModuleEnd or LayoutKind.Header;
}
