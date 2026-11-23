using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Ax3.Data.Layout;
using Xunit;

namespace HspDecompiler.Core.Tests.Ax3;

public class DebugInfoTests
{
    private const byte File = 254;
    private const byte End = 255;

    private static DebugInfo Read(params byte[] segment) =>
        DebugInfo.Read(segment, _ => [], _ => "start.hsp"u8.ToArray());

    [Fact]
    public void RewindPutsExpandedModuleGotoOnTheModuleLine()
    {
        // "gcount = 1" on line 1, "#module m" on line 2 expanded to its goto on line 3, then "##2".
        DebugInfo info = Read(File, 1, 0, 0, 0, 0, 0, 6, 0, 4, File, 0, 0, 0, 2, 0, 0, 6, End);

        DebugLine jump = info.Lines.Find(line => line.CodeOffset == 6 && line.CodeSize == 4);

        Assert.Equal(2, jump.Line);
        Assert.Equal(3, info.Lines[^1].Line);
    }

    [Fact]
    public void MidLineFileRecordKeepsTheStatementOnItsFirstLine()
    {
        // A {"..."} string that starts on line 1 and ends on line 3.
        DebugInfo info = Read(File, 1, 0, 0, 0, 0, 0, File, 0, 0, 0, 3, 0, 8, End);

        DebugLine statement = info.Lines[^1];

        Assert.Equal(1, statement.Line);
        Assert.Equal([3], statement.StringEnds!);
        Assert.Equal([2], LongStrings.Breaks(statement));
    }

    [Fact]
    public void LongStringTakesBackRecordedLineBreaks()
    {
        string text = LongStrings.Render("s = \"multi\\nline\"", new([1]));

        Assert.Equal("s = {\"multi\nline\"}", text);
    }
}
