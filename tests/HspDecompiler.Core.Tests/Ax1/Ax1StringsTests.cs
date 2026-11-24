using System.Linq;
using HspDecompiler.Core.Ax1.Writing;
using HspDecompiler.Core.Encoding;
using Xunit;

namespace HspDecompiler.Core.Tests.Ax1;

public class Ax1StringsTests
{
    private static Ax1Literal Spell(string text) =>
        Ax1Strings.Spell(ShiftJisHelper.Encoding.GetBytes(text));

    [Theory]
    [InlineData("abc", "\"abc\"")]
    [InlineData("a\r\nb\tc", "\"a\\nb\\tc\"")]
    [InlineData("a\\\\b", "\"a\\\\b\"")]
    [InlineData("a\\\"", "\"a\\\"")]
    [InlineData("", "\"\"")]
    public void StoredStringSpellsAsOneLiteral(string stored, string expected)
    {
        // Act
        Ax1Literal literal = Spell(stored);

        // Assert
        Assert.True(literal.Exact);
        string[] pieces = [.. literal.Pieces];
        Assert.Equal([expected], pieces);
    }

    [Fact]
    public void KeptQuoteInsideTheStringEndsAPiece()
    {
        // Act
        Ax1Literal literal = Spell("ab\\\"cd");

        // Assert
        Assert.True(literal.Exact);
        string[] pieces = [.. literal.Pieces];
        Assert.Equal(["\"ab\\\"", "\"cd\""], pieces);
    }

    [Fact]
    public void LongStringSplitsIntoPiecesAContinuationLineHolds()
    {
        // Arrange
        string stored = string.Concat(Enumerable.Repeat("日本語", 100));

        // Act
        Ax1Literal literal = Spell(stored);

        // Assert
        Assert.True(literal.Exact);
        Assert.True(literal.Pieces.Length > 1);
        Assert.All(literal.Pieces, p =>
            Assert.True(ShiftJisHelper.Encoding.GetByteCount(p) <= Ax1Strings.MaxLiteral)
        );
        Assert.Equal(stored, string.Concat(literal.Pieces.Select(p => p[1..^1])));
    }

    [Fact]
    public void DetachedLiteralMovesTheWholeStringIntoContinuations()
    {
        // Act
        Ax1Literal literal = Spell("abc").Detached();

        // Assert
        Assert.Equal(Ax1Literal.Empty, literal.Inline);
        string[] pieces = [.. literal.Continuations];
        Assert.Equal(["\"abc\""], pieces);
    }

    [Theory]
    [InlineData("a\rb")]
    [InlineData("a\"b")]
    [InlineData("a\\n")]
    public void BytesNoLiteralProducesAreNotExact(string stored)
    {
        // Act
        Ax1Literal literal = Spell(stored);

        // Assert
        Assert.False(literal.Exact);
    }
}
