using HspDecompiler.Core.Pipeline;
using HspDecompiler.Core.Releases;

namespace HspDecompiler.Core.Ax3.Data;

// Everything a line's text depends on besides the line: the module it sits in, the global
// functions that decide how names inside a module are spelled, the style the caller chose, and
// the releases that could have written the AX. Rendering reads nothing else, so a line renders
// the same however many passes ran before.
//
// 行のテキストが行以外に依存するものすべて。行が属するモジュール、モジュール内の名前の書き方を
// 決めるグローバル関数、呼び出し側が選んだ書き方、そして AX を書いた可能性のあるリリース。
// 出力はこれ以外を読まないため、それまでに何度出力しても行は同じに書かれる。
internal sealed record RenderContext(
    ModuleScope Scope,
    string? Module,
    SourceStyle Style,
    ReleaseProfile Release
)
{
    internal static RenderContext Of(AxData data) =>
        new(data.Scope, null, data.Style, data.Release);

    internal RenderContext In(string? module) => this with { Module = module };

    internal string Variable(string name) => Scope.Variable(name, Module);

    internal string Function(string name) => ModuleScope.Function(name, Module);

    internal string Import(string name) => ModuleScope.Import(name, Module);

    internal string Label(string name, string? owner) => ModuleScope.Label(name, owner, Module);
}

// Renders lines in output order, entering a module at its #module and leaving it at #global.
// Each pass over the lines takes a new renderer, so no pass starts inside a module another
// left open.
//
// 行を出力順に書き、#module でモジュールに入り #global で出る。行をたどるたびに新しい
// レンダラーを使うため、別の出力が開いたままにしたモジュールの中から始まることは無い。
internal sealed class LineRenderer(RenderContext context)
{
    private RenderContext _context = context;

    internal string Render(LogicalLine line)
    {
        if (line is Line.EndOfModule)
        {
            _context = _context.In(null);
        }

        string text = line.Render(_context);
        if (line is Line.ModuleStart start)
        {
            _context = _context.In(start.Name);
        }

        return text;
    }
}
