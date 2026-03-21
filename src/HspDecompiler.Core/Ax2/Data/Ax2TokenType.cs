namespace HspDecompiler.Core.Ax2.Data;

internal static class Ax2TokenType
{
    internal const int Operator = 0x00;
    internal const int Integer = 0x08;
    internal const int String = 0x10;
    internal const int Label = 0x18;
    internal const int Variable = 0x20;
    internal const int SystemVariable = 0x38;
    internal const int FlowControl = 0x40;
    internal const int GuiFunction = 0x48;
    internal const int UserFunction = 0x50;
    internal const int IfElse = 0x58;
    internal const int Deffunc = 0x60;
    internal const int EndProgram = 0x78;

    internal const int TypeMask = 0x78;
    internal const int ExtendedValueBit = 0x01;
    internal const int ExtendedValueCarry = 256;
    internal const int LongValueFlag = 0x80;
    internal const int LineHeadFlag = 0x02;
    internal const int ArgFlag = 0x04;
}
