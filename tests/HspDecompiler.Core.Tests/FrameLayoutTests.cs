using HspDecompiler.Core.Releases;
using Xunit;

namespace HspDecompiler.Core.Tests;

public class FrameLayoutTests
{
    [Theory]
    [InlineData(new short[] { -3, 4 }, 12, false)]
    [InlineData(new short[] { -3, 4 }, 20, true)]
    [InlineData(new short[] { -7, -1 }, 60, false)]
    [InlineData(new short[] { -7, -1 }, 88, true)]
    public void PointerSizedParametersNameTheLayout(short[] types, int size, bool wide) =>
        Assert.Equal(wide ? FrameLayout.Wide : FrameLayout.Narrow, FrameLayouts.Of(types, size));

    [Theory]
    [InlineData(new short[] { 4, -6, 3 }, 16)]
    [InlineData(new short[] { -3 }, 10)]
    [InlineData(new short[] { 99 }, 8)]
    public void SameSizesOrUnknownTypesNameNone(short[] types, int size) =>
        Assert.Null(FrameLayouts.Of(types, size));
}
