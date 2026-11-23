using System.Collections.Generic;
using System.Linq;
using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Ax3.Data.Layout;
using HspDecompiler.Core.Ax3.Data.Line;
using Xunit;

namespace HspDecompiler.Core.Tests.Ax3;

public class MarkedLayoutTests
{
    private static MarkedRow Row(RowKind kind, params int[] escapes) =>
        new(new CommentLine("x"), kind, escapes);

    private static MarkedItem Item(LogicalLine line, RowKind kind, int target = 0) =>
        new(line, kind, []) { Target = target };

    private static List<List<string>>? Place(List<MarkedItem> items, params string[] headers) =>
        new MarkedPlacer(new ModuleScope(), [.. headers]).Place(items, part => $"x_{part}.hsp");

    [Fact]
    public void BlankRowsTakeNoLineAndCodeJoinsCodeOrLabel()
    {
        var table = new RowTable([
            Row(RowKind.Label),
            Row(RowKind.Code),
            Row(RowKind.Blank),
            Row(RowKind.Close),
            Row(RowKind.Directive),
            Row(RowKind.Code),
        ]);

        int[] widths = [.. Enumerable.Range(0, 6).Select(table.Width)];

        Assert.Equal([1, 0, 0, 0, 1, 1], widths);
        Assert.Equal(3, table.Before(6));
        Assert.Equal(3, table.NextFilled(2));
    }

    [Fact]
    public void StringRecordTakesTheLiteralWhoseBreaksReachIt()
    {
        var table = new RowTable([Row(RowKind.Directive), Row(RowKind.Code, 0, 2)]);
        var events = MarkEvents.Build(table, new AxData(), false, false);

        MarkChoice[]? choices = MarkMatcher.Match(table, events, [4]);

        Assert.NotNull(choices);
        Assert.Equal(1, events.Events[choices[0].Event].Ordinal);
        Assert.False(choices[0].NewPart);
    }

    [Fact]
    public void RecordBeforeItsRowsCanReachStartsANewFile()
    {
        var table = new RowTable([
            Row(RowKind.Directive),
            Row(RowKind.Directive),
            Row(RowKind.Directive),
            Row(RowKind.Code, 0),
        ]);
        var events = MarkEvents.Build(table, new AxData(), false, false);

        MarkChoice[]? choices = MarkMatcher.Match(table, events, [1]);

        Assert.NotNull(choices);
        Assert.True(choices[0].NewPart);
    }

    [Fact]
    public void RecordWithoutAnyEventFindsNoLayout()
    {
        var table = new RowTable([Row(RowKind.Code)]);
        var events = MarkEvents.Build(table, new AxData(), false, false);

        Assert.Null(MarkMatcher.Match(table, events, [1]));
    }

    [Fact]
    public void DirectiveStandsOnItsRecordedLine()
    {
        List<List<string>>? parts = Place([
            Item(new CommentLine("a"), RowKind.Directive),
            Item(new ModuleStart(string.Empty, true, null, -1), RowKind.Directive, 4),
            Item(new EndOfModule(-1), RowKind.Directive, 5),
        ]);

        Assert.NotNull(parts);
        Assert.Equal(["//a", string.Empty, string.Empty, "#module", "#global"], parts[0]);
    }

    [Fact]
    public void BlankRowsDropWhenTheLinesAreShort()
    {
        List<List<string>>? parts = Place([
            Item(new CommentLine(), RowKind.Blank),
            Item(new CommentLine("a"), RowKind.Directive),
            Item(new ModuleStart("m", false, null, -1), RowKind.Directive, 2),
        ]);

        Assert.NotNull(parts);
        Assert.Equal(["//a", "#module m"], parts[0]);
    }

    [Fact]
    public void DirectiveWithoutRoomFindsNoLayout()
    {
        List<List<string>>? parts = Place([
            Item(new CommentLine("a"), RowKind.Directive),
            Item(new CommentLine("b"), RowKind.Directive),
            Item(new ModuleStart("m", false, null, -1), RowKind.Directive, 2),
        ]);

        Assert.Null(parts);
    }

    [Fact]
    public void NewPartEndsThePreviousFileWithItsInclude()
    {
        MarkedItem restart = Item(new ModuleStart("b", false, null, -1), RowKind.Directive, 1);
        restart.StartsPart = true;

        List<List<string>>? parts = Place([
            Item(new ModuleStart("a", false, null, -1), RowKind.Directive, 3),
            Item(new EndOfModule(-1), RowKind.Directive, 4),
            restart,
            Item(new EndOfModule(-1), RowKind.Directive, 2),
        ]);

        Assert.NotNull(parts);
        Assert.Equal(
            [string.Empty, string.Empty, "#module a", "#global", "#include \"x_1.hsp\""],
            parts[0]
        );
        Assert.Equal(["#module b", "#global"], parts[1]);
    }

    [Fact]
    public void StringEndsOnItsRecordedLine()
    {
        var row = new MarkedItem(new CommentLine(" s = \"a\\nb\\nc\""), RowKind.Code, [2])
        {
            Ends = new SortedDictionary<int, int> { [0] = 3 },
        };

        List<List<string>>? parts = Place([
            Item(new ModuleStart("m", false, null, -1), RowKind.Directive, 1),
            row,
        ]);

        Assert.NotNull(parts);
        Assert.Equal(["#module m", "// s = {\"a", "b\\nc\"}"], parts[0]);
    }

    [Fact]
    public void HeadersWithoutRoomGoAfterTheCode()
    {
        List<List<string>>? parts = Place(
            [Item(new ModuleStart("m", false, null, -1), RowKind.Directive, 1)],
            "#runtime \"hsp3cl\""
        );

        Assert.NotNull(parts);
        Assert.Equal(["#module m", "#runtime \"hsp3cl\""], parts[0]);
    }
}
