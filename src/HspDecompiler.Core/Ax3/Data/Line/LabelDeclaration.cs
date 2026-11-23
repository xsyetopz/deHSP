using HspDecompiler.Core.Ax3.Data.PP;

namespace HspDecompiler.Core.Ax3.Data.Line;

internal sealed class PreprocessorDeclaration : LogicalLine
{
    internal PreprocessorDeclaration(Preprocessor pp)
    {
        _pp = pp;
    }

    private readonly Preprocessor? _pp;

    internal Preprocessor? Declared => _pp;

    internal override int TabCount => 0;

    internal override int TokenOffset => -1;

    internal override string Render(RenderContext context) => _pp!.Render(context);
}
