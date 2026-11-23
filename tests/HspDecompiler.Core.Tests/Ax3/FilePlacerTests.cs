using System.Collections.Generic;
using HspDecompiler.Core.Ax3.Data.Layout;
using Xunit;

namespace HspDecompiler.Core.Tests.Ax3;

public class FilePlacerTests
{
    private static LayoutItem Item(LayoutKind kind, string text, int target = 0) =>
        new(kind, text, 0, 1, target);

    [Fact]
    public void StatementsOfOneLineJoin()
    {
        var placer = new FilePlacer();

        List<string> lines = placer.Place(
            [Item(LayoutKind.Code, "a = 1", 1), Item(LayoutKind.Code, "b = 2", 1)],
            1
        );

        Assert.Equal(["a = 1 : b = 2"], lines);
    }

    [Fact]
    public void ModuleStartNeverJoinsCodeOnItsLine()
    {
        var placer = new FilePlacer();

        List<string> lines = placer.Place(
            [Item(LayoutKind.Code, "gcount = 1", 1), Item(LayoutKind.ModuleStart, "#module m", 1)],
            2
        );

        Assert.Equal(["gcount = 1", "#module m"], lines);
        Assert.Equal(1, placer.Moved);
    }

    [Fact]
    public void HeaderTakesFirstGapWithRoom()
    {
        var placer = new FilePlacer();

        List<string> lines = placer.Place(
            [
                Item(LayoutKind.Header, "#bootopt hsp64 1"),
                Item(LayoutKind.Code, "a = 1", 1),
                Item(LayoutKind.Blank, string.Empty),
                Item(LayoutKind.Code, "b = 2", 4),
            ],
            4
        );

        Assert.Equal(["a = 1", "#bootopt hsp64 1", string.Empty, "b = 2"], lines);
        Assert.Equal(0, placer.Moved);
    }

    [Fact]
    public void HeaderWithoutGapGoesAfterLastLine()
    {
        var placer = new FilePlacer();

        List<string> lines = placer.Place(
            [
                Item(LayoutKind.Header, "#bootopt hsp64 1"),
                Item(LayoutKind.Code, "a = 1", 1),
                Item(LayoutKind.Code, "b = 2", 2),
            ],
            2
        );

        Assert.Equal(["a = 1", "b = 2", "#bootopt hsp64 1"], lines);
        Assert.Equal(0, placer.Moved);
    }

    [Fact]
    public void MultilineStringTakesItsLines()
    {
        var placer = new FilePlacer();

        List<string> lines = placer.Place(
            [Item(LayoutKind.Code, "s = {\"x\ny\"}", 1), Item(LayoutKind.Code, "mes s", 3)],
            3
        );

        Assert.Equal(["s = {\"x", "y\"}", "mes s"], lines);
    }
}
