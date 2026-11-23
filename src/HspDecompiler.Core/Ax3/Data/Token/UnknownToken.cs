namespace HspDecompiler.Core.Ax3.Data.Token;

internal sealed class UnknownToken : CodeToken
{
    internal UnknownToken(PrimitiveToken token)
    {
        _token = token;
    }

    private readonly PrimitiveToken? _token;
    internal override int TokenOffset => _token!.TokenOffset;

    internal override string Render(RenderContext context) => " /*" + _token + "*/";
}
