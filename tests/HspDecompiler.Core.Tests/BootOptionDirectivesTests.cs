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

    [Theory]
    [InlineData(0u, false)]
    [InlineData(BootOptionDirectives.Utf8, true)]
    [InlineData(BootOptionDirectives.Hsp64, true)]
    [InlineData(BootOptionDirectives.NoMmTimer, false)]
    public void Utf8RuntimeComesFromTheUtf8OrHsp64Bit(uint word, bool expected) =>
        Assert.Equal(expected, BootOptionDirectives.IsUtf8Runtime(word));

    [Fact]
    public void Hsp64Utf8StringsWithoutTheBitWriteNoUtf8Directive()
    {
        // hspcmp 3.8 beta 2 gives HSP64 programs UTF-8 strings without setting the UTF-8 bit.
        //
        // hspcmp 3.8 beta 2 は UTF-8 ビットを立てずに HSP64 のプログラムへ UTF-8 の文字列を与える。
        Assert.Equal(
            ["#bootopt hsp64 1"],
            BootOptionDirectives.Build(
                BootOptionDirectives.Hsp64,
                utf8Strings: true,
                usesAwait: false
            )
        );
    }

    [Fact]
    public void Hsp64WithTheUtf8BitKeepsCmpopt()
    {
        Assert.Equal(
            ["#cmpopt utf8 1", "#bootopt hsp64 1"],
            BootOptionDirectives.Build(
                BootOptionDirectives.Utf8 | BootOptionDirectives.Hsp64,
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
