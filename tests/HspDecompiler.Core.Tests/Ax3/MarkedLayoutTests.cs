using System.Collections.Generic;
using System.Linq;
using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Ax3.Data.Layout;
using HspDecompiler.Core.Ax3.Data.Line;
using HspDecompiler.Core.Releases;
using Xunit;

namespace HspDecompiler.Core.Tests.Ax3;

public class MarkedLayoutTests
{
    private static AxData Hsp33() => new() { Release = new([CompilerReleases.Hsp33]) };

    private static MarkedRow Row(LineKind kind, params int[] escapes) =>
        new(new CommentLine("x"), kind, escapes);

    private static MarkedItem Item(LogicalLine line, LineKind kind, int target = 0) =>
        new(line, kind, []) { Target = target };

    private static List<List<string>>? Place(List<MarkedItem> items, params string[] headers) =>
        new MarkedPlacer(RenderContext.Of(Hsp33()), [.. headers]).Place(items, part =>
            $"x_{part}.hsp"
        );

    [Fact]
    public void BlankRowsTakeNoLineAndCodeJoinsCodeOrLabel()
    {
        var table = new RowTable([
            Row(LineKind.Label),
            Row(LineKind.Code),
            Row(LineKind.Blank),
            Row(LineKind.Close),
            Row(LineKind.Directive),
            Row(LineKind.Code),
        ]);

        int[] widths = [.. Enumerable.Range(0, 6).Select(table.Width)];

        Assert.Equal([1, 0, 0, 0, 1, 1], widths);
        Assert.Equal(3, table.Before(6));
        Assert.Equal(3, table.NextFilled(2));
    }

    [Fact]
    public void StringRecordTakesTheLiteralWhoseBreaksReachIt()
    {
        var table = new RowTable([Row(LineKind.Directive), Row(LineKind.Code, 0, 2)]);
        var events = MarkEvents.Build(table, Hsp33(), false, false);

        MarkChoice[]? choices = MarkMatcher.Match(table, events, [4], false);

        Assert.NotNull(choices);
        Assert.Equal(1, events.Events[choices[0].Event].Ordinal);
        Assert.False(choices[0].NewPart);
    }

    [Fact]
    public void StringTakesNoRecordWhenSomeCandidateDoesNotRecordStrings()
    {
        var table = new RowTable([Row(LineKind.Directive), Row(LineKind.Code, 0, 2)]);
        var data = new AxData { Release = new([CompilerReleases.Hsp321, CompilerReleases.Hsp33]) };
        var events = MarkEvents.Build(table, data, false, false);

        MarkChoice[]? choices = MarkMatcher.Match(table, events, [4], true);

        Assert.Empty(events.Events);
        Assert.NotNull(choices);
        Assert.True(choices[0].Free);
    }

    [Fact]
    public void RecordBeforeItsRowsCanReachStartsANewFile()
    {
        var table = new RowTable([
            Row(LineKind.Directive),
            Row(LineKind.Directive),
            Row(LineKind.Directive),
            Row(LineKind.Code, 0),
        ]);
        var events = MarkEvents.Build(table, Hsp33(), false, false);

        MarkChoice[]? choices = MarkMatcher.Match(table, events, [1], false);

        Assert.NotNull(choices);
        Assert.True(choices[0].NewPart);
    }

    [Fact]
    public void RecordWithoutAnyEventFindsNoLayout()
    {
        var table = new RowTable([Row(LineKind.Code)]);
        var events = MarkEvents.Build(table, Hsp33(), false, false);

        Assert.Null(MarkMatcher.Match(table, events, [1], false));
    }

    [Fact]
    public void RecordWithoutAnyEventIsFreeAfterTheMacroDefinition()
    {
        var table = new RowTable([Row(LineKind.Code), Row(LineKind.Code)]);
        var events = MarkEvents.Build(table, Hsp33(), false, false);

        MarkChoice[]? choices = MarkMatcher.Match(table, events, [3], true);

        Assert.NotNull(choices);
        Assert.Equal(new MarkChoice(-1, false, true), choices[0]);
    }

    [Fact]
    public void FreeRecordNeedsALineBetweenTheDefinitionAndItself()
    {
        var table = new RowTable([Row(LineKind.Code)]);
        var events = MarkEvents.Build(table, Hsp33(), false, false);

        Assert.Null(MarkMatcher.Match(table, events, [2], true));
    }

    [Fact]
    public void FreeRecordFollowsTheRowsOfTheEventBeforeIt()
    {
        var table = new RowTable([Row(LineKind.Code, 1), Row(LineKind.Code)]);
        var events = MarkEvents.Build(table, Hsp33(), false, false);

        MarkChoice[]? choices = MarkMatcher.Match(table, events, [3, 5], true);

        Assert.NotNull(choices);
        Assert.False(choices[0].Free);
        Assert.Equal(new MarkChoice(0, false, true), choices[1]);
    }

    [Fact]
    public void LineRecordMacroUseStandsOnTheLineBeforeItsRecord()
    {
        List<List<string>>? parts = Place([
            Item(LineRecordMacro.Definition(), LineKind.Directive, 1),
            Item(new CommentLine("a"), LineKind.Code),
            Item(LineRecordMacro.Use(), LineKind.Directive, 3),
            Item(new CommentLine("b"), LineKind.Code),
        ]);

        Assert.NotNull(parts);
        Assert.Equal(
            ["#define global __line_record(%1) %c", "//a", "\t__line_record", "//b"],
            parts[0]
        );
    }

    [Fact]
    public void DirectiveStandsOnItsRecordedLine()
    {
        List<List<string>>? parts = Place([
            Item(new CommentLine("a"), LineKind.Directive),
            Item(new ModuleStart(string.Empty, true, null, -1), LineKind.ModuleStart, 4),
            Item(new EndOfModule(-1), LineKind.ModuleEnd, 5),
        ]);

        Assert.NotNull(parts);
        Assert.Equal(["//a", string.Empty, string.Empty, "#module", "#global"], parts[0]);
    }

    [Fact]
    public void BlankRowsDropWhenTheLinesAreShort()
    {
        List<List<string>>? parts = Place([
            Item(new CommentLine(), LineKind.Blank),
            Item(new CommentLine("a"), LineKind.Directive),
            Item(new ModuleStart("m", false, null, -1), LineKind.ModuleStart, 2),
        ]);

        Assert.NotNull(parts);
        Assert.Equal(["//a", "#module m"], parts[0]);
    }

    [Fact]
    public void DirectiveWithoutRoomFindsNoLayout()
    {
        List<List<string>>? parts = Place([
            Item(new CommentLine("a"), LineKind.Directive),
            Item(new CommentLine("b"), LineKind.Directive),
            Item(new ModuleStart("m", false, null, -1), LineKind.ModuleStart, 2),
        ]);

        Assert.Null(parts);
    }

    [Fact]
    public void NewPartEndsThePreviousFileWithItsInclude()
    {
        MarkedItem restart = Item(new ModuleStart("b", false, null, -1), LineKind.ModuleStart, 1);
        restart.StartsPart = true;

        List<List<string>>? parts = Place([
            Item(new ModuleStart("a", false, null, -1), LineKind.ModuleStart, 3),
            Item(new EndOfModule(-1), LineKind.ModuleEnd, 4),
            restart,
            Item(new EndOfModule(-1), LineKind.ModuleEnd, 2),
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
        var row = new MarkedItem(new CommentLine(" s = \"a\\nb\\nc\""), LineKind.Code, [2])
        {
            Ends = new SortedDictionary<int, int> { [0] = 3 },
        };

        List<List<string>>? parts = Place([
            Item(new ModuleStart("m", false, null, -1), LineKind.ModuleStart, 1),
            row,
        ]);

        Assert.NotNull(parts);
        Assert.Equal(["#module m", "// s = {\"a", "b\\nc\"}"], parts[0]);
    }

    [Fact]
    public void HeadersWithoutRoomGoAfterTheCode()
    {
        List<List<string>>? parts = Place(
            [Item(new ModuleStart("m", false, null, -1), LineKind.ModuleStart, 1)],
            "#runtime \"hsp3cl\""
        );

        Assert.NotNull(parts);
        Assert.Equal(["#module m", "#runtime \"hsp3cl\""], parts[0]);
    }
}
