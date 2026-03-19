namespace HspDecompiler.Core.Ax3.Data.Line
{
    // Fix #26: renamed from ScoopEnd → ScopeEnd
    internal sealed class ScopeEnd : LogicalLine
    {
        internal override bool TabDecrement
        {
            get
            {
                return true;
            }
        }
        internal override int TokenOffset
        {
            get { return -1; }
        }

        public override string ToString()
        {
            return "}";
        }
    }
}
