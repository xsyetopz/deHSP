using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Encoding;
using Xunit;

namespace HspDecompiler.Core.Tests;

public class ScriptStringsTests
{
    private static readonly byte[] s_utf8Text = "日本"u8.ToArray();
    private static readonly byte[] s_shiftJisText = ShiftJisHelper.Encoding.GetBytes("日本");

    private static ScriptStrings Resolved(bool hasRuntime, bool utf8Runtime, params byte[][] texts)
    {
        var strings = new ScriptStrings();
        foreach (byte[] text in texts)
        {
            strings.Add(text);
        }

        strings.Resolve(hasRuntime, utf8Runtime);
        return strings;
    }

    [Fact]
    public void ValidUtf8WithoutRuntimeResolvesToUtf8()
    {
        ScriptStrings strings = Resolved(false, false, s_utf8Text);

        Assert.True(strings.IsUtf8);
        Assert.Equal("日本", strings.Decode(s_utf8Text));
    }

    [Fact]
    public void ValidUtf8WithRuntimeNeedsAUtf8Runtime()
    {
        Assert.False(Resolved(true, false, s_utf8Text).IsUtf8);
        Assert.True(Resolved(true, true, s_utf8Text).IsUtf8);
    }

    [Fact]
    public void OneShiftJisStringKeepsShiftJis()
    {
        ScriptStrings strings = Resolved(false, true, s_utf8Text, s_shiftJisText);

        Assert.False(strings.IsUtf8);
        Assert.Equal("日本", strings.Decode(s_shiftJisText));
    }

    [Fact]
    public void AsciiOnlyStaysShiftJis() =>
        Assert.False(Resolved(false, true, "abc"u8.ToArray()).IsUtf8);

    [Fact]
    public void EscapeWritesLiteralBodyAndDropsCarriageReturn() =>
        Assert.Equal(@"a\\b\""c\td\ne", ScriptStrings.Escape("a\\b\"c\td\r\ne"));
}
