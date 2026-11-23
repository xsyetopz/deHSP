using System.Collections.Generic;
using System.Linq;
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
        Ax2Source source = IncludeLayout.Split(lines, [], "main.as");

        // Assert
        Assert.Equal(["mes 1", "stop"], source.Lines);
        Assert.Empty(source.Includes);
    }
}
