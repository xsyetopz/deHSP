namespace HspDecompiler.Core.Ax3.Data.Line;

internal class EndOfModule : LogicalLine
{
    internal EndOfModule(int exitOffset)
    {
        ExitOffset = exitOffset;
    }

    // The code offset of the exit label hspcmp put here, which tells whether an #include
    // beside the #global came before it or after it. TokenOffset stays -1 because the line
    // itself compiles to no code.
    //
    // hspcmp がここに置いた終了ラベルのコード位置。#global の隣の #include がその前か後かが
    // これで分かる。行自体はコードを生まないため TokenOffset は -1 のままにする。
    internal int ExitOffset { get; }

    internal override int TokenOffset => -1;
    internal override int TabCount => 0;

    public override string ToString() => "#global";
}
