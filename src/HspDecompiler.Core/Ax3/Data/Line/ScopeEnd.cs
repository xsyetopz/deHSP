namespace HspDecompiler.Core.Ax3.Data.Line;

// Fix #26: renamed from ScoopEnd → ScopeEnd
internal sealed class ScopeEnd : LogicalLine
{
    internal override bool TabDecrement => true;
    internal override int TokenOffset => -1;

    public override string ToString() => "}";
}
