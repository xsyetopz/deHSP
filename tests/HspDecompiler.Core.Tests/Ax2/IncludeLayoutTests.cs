using System.Collections.Generic;
using System.Linq;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax2.Data;
using Xunit;

namespace HspDecompiler.Core.Tests.Ax2;

public class IncludeLayoutTests
{
    [Theory]
    [InlineData("sub.as", true)]
    [InlineData("SUB.AS", true)]
    [InlineData("main.hsp", true)]
    [InlineData("image.bmp", false)]
    [InlineData("as", false)]
    public void IsSourceFileNameAcceptsHspSourceExtensions(string text, bool expected) =>
        Assert.Equal(expected, IncludeLayout.IsSourceFileName(text));

    [Fact]
    public void MergePlacesDeclarationRightAfterPrecedingAnchoredCodeLine()
    {
        // Arrange
        List<SourceLine> code = [new("a", 10), new("b", -1), new("c", 30)];
        List<SourceLine> declarations = [new("#func f", 20)];

        // Act
        List<SourceLine> merged = IncludeLayout.Merge(code, declarations);

        // Assert
        Assert.Equal(["a", "#func f", "b", "c"], merged.Select(line => line.Text));
    }

    [Fact]
    public void MergePutsDeclarationsBeforeFirstAnchorAndAfterLastAnchor()
    {
        // Arrange
        List<SourceLine> code = [new("a", 10), new("b", 20)];
        List<SourceLine> declarations = [new("#func early", 5), new("#func late", 40)];

        // Act
        List<SourceLine> merged = IncludeLayout.Merge(code, declarations);

        // Assert
        Assert.Equal(["#func early", "a", "b", "#func late"], merged.Select(line => line.Text));
    }

    [Fact]
    public void SplitWithoutFileSwitchesKeepsEveryLineInMainSource()
    {
        // Arrange
        List<SourceLine> lines = [new("mes 1", 0), new("stop", -1)];

        // Act
        DecodedSource source = IncludeLayout.Split(lines, [], "main.as", true);

        // Assert
        Assert.Equal(["mes 1", "stop"], source.Main);
        Assert.Empty(source.Includes);
    }

    [Fact]
    public void SplitEndsIncludeAfterItsGlobalLine()
    {
        // Arrange
        List<SourceLine> lines =
        [
            new("#module", 10),
            new("#deffunc f", 10),
            new("return", -1),
            new("#global", -1),
            new("f", -1),
            new("stop", -1),
        ];
        List<FileSwitch> switches = [new(0, "main.as"), new(5, "mod.as")];

        // Act
        DecodedSource source = IncludeLayout.Split(lines, switches, "main.as", false);

        // Assert
        Assert.Equal(["#include \"mod.as\"", "f", "stop"], source.Main);
        Assert.Equal(
            ["#module", "#deffunc f", "return", "#global"],
            source.Includes.Single().Lines
        );
    }

    [Fact]
    public void SplitReturnSkippingALevelClosesEveryInnerInclude()
    {
        // Arrange
        List<SourceLine> lines =
        [
            new("mes \"a\"", 2),
            new("mes \"b\"", 30),
            new("*next", -1),
            new("mes \"c\"", 50),
        ];
        List<FileSwitch> switches =
        [
            new(0, "main.as"),
            new(5, "outer.as"),
            new(20, "inner.as"),
            new(40, "main.as"),
        ];

        // Act
        DecodedSource source = IncludeLayout.Split(lines, switches, "main.as", false);

        // Assert
        Assert.Equal(["mes \"a\"", "#include \"outer.as\"", "*next", "mes \"c\""], source.Main);
        Assert.Equal(["inner.as", "outer.as"], source.Includes.Select(f => f.Name));
    }
}
