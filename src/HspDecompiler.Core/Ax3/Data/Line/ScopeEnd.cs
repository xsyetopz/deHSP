namespace HspDecompiler.Core.Ax3.Data.Line;

internal sealed class ScopeEnd : LogicalLine
{
    internal override bool TabDecrement => true;
    internal override int TokenOffset => -1;

    internal override string Render(RenderContext context) => "}";
}
