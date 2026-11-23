using System.Globalization;
using HspDecompiler.Core.Ax3.Data.Primitive;
using Xunit;

namespace HspDecompiler.Core.Tests.Ax3;

public class DoubleLiteralFormatTests
{
    [Theory]
    [InlineData(1.0, "1.0")]
    [InlineData(-3.0, "-3.0")]
    [InlineData(0.1, "0.1")]
    [InlineData(1e300, "1e+300")]
    [InlineData(1.5e-7, "1.5e-07")]
    public void FormatsAsDoubleLiteralHspcmpParses(double value, string expected)
    {
        string text = DoublePrimitive.FormatLiteral(value);

        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(1.0 / 3.0)]
    [InlineData(123456789.125)]
    [InlineData(5e-324)]
    public void FormattedTextParsesBackToSameBits(double value)
    {
        string text = DoublePrimitive.FormatLiteral(value);

        Assert.Equal(value, double.Parse(text, CultureInfo.InvariantCulture));
    }
}
