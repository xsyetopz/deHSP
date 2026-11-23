namespace HspDecompiler.Core.Ax3.Data;

internal abstract class CodeToken
{
    internal abstract int TokenOffset { get; }

    internal abstract string Render(RenderContext context);

    internal virtual void CheckLabel() { }

    internal virtual bool CheckRpn() => true;
}
