using HspDecompiler.Core.Ax3;
using Xunit;

namespace HspDecompiler.Core.Tests;

public class BootOptionDirectivesTests
{
    [Fact]
    public void ClearWordYieldsNoDirective() =>
        Assert.Empty(BootOptionDirectives.Build(0, utf8Strings: false, usesAwait: false));

    [Fact]
    public void Utf8StringsUseCmpoptInsteadOfBootopt()
    {
        Assert.Equal(
            ["#cmpopt utf8 1"],
            BootOptionDirectives.Build(
                BootOptionDirectives.Utf8,
                utf8Strings: true,
                usesAwait: false
            )
        );
    }

    [Fact]
    public void Utf8BitWithShiftJisStringsUsesBootopt()
    {
        Assert.Equal(
            ["#bootopt utf8 1"],
            BootOptionDirectives.Build(
                BootOptionDirectives.Utf8,
                utf8Strings: false,
                usesAwait: false
            )
        );
    }

    [Fact]
    public void PlainBitsKeepHeaderOrder()
    {
        const uint word =
            BootOptionDirectives.IoResume
            | BootOptionDirectives.NoGdip
            | BootOptionDirectives.Hsp64
            | BootOptionDirectives.Float32
            | BootOptionDirectives.OrgRnd;

        Assert.Equal(
            [
                "#bootopt hsp64 1",
                "#bootopt nogdip 1",
                "#bootopt float32 1",
                "#bootopt orgrnd 1",
                "#bootopt ioresume 1",
            ],
            BootOptionDirectives.Build(word, utf8Strings: false, usesAwait: false)
        );
    }

    [Fact]
    public void NoTimerWithoutAwaitIsLeftToTheCompiler()
    {
        // hspcmp sets NOMMTIMER by itself when the code has no await.
        //
        // コードに await が無ければ hspcmp が自分で NOMMTIMER を立てる。
        Assert.Empty(
            BootOptionDirectives.Build(
                BootOptionDirectives.NoMmTimer,
                utf8Strings: false,
                usesAwait: false
            )
        );
    }

    [Fact]
    public void NoTimerBesideAwaitIsWritten()
    {
        Assert.Equal(
            ["#bootopt notimer 1"],
            BootOptionDirectives.Build(
                BootOptionDirectives.NoMmTimer,
                utf8Strings: false,
                usesAwait: true
            )
        );
    }
}
