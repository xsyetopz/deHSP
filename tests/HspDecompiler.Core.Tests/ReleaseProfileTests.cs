using System.Linq;
using HspDecompiler.Core.Releases;
using Xunit;

namespace HspDecompiler.Core.Tests;

public class ReleaseProfileTests
{
    private static ReleaseProfile Infer(
        uint word,
        int marks = 0,
        bool flags = false,
        FrameLayout? frames = null
    ) => ReleaseProfile.Infer(new AxEvidence(word, marks, flags, frames));

    [Theory]
    [InlineData(0x300u, false, false, false)]
    [InlineData(0x301u, true, false, false)]
    [InlineData(0x350u, true, false, true)]
    [InlineData(0x360u, true, true, true)]
    public void FormatWordAloneGivesTheThresholdsItAlwaysHad(
        uint word,
        bool drops,
        bool pools,
        bool optprm
    )
    {
        ReleaseProfile profile = Infer(word);

        Assert.Equal(drops, profile.DropsUnused);
        Assert.Equal(pools, profile.PoolsStrings);
        Assert.Equal(optprm, profile.KnowsOptprm);
    }

    [Fact]
    public void FormatWordSharedByReleasesKeepsEachOfThem()
    {
        Assert.Equal("3.1-3.4", Infer(0x301).ToString());
        Assert.Equal("3.6-3.8β2 x64", Infer(0x360).ToString());
    }

    [Fact]
    public void NormalBuildRecordsNarrowToTheReleasesThatWriteThem()
    {
        ReleaseProfile profile = Infer(0x301, marks: 2);

        Assert.Equal("3.1-3.32", profile.ToString());
        Assert.True(profile.MayWriteNormalBuildRecords);
        Assert.False(profile.RecordsStringEnds);
    }

    [Fact]
    public void OptprmFlagsWithRecordsMeanStringEndsAreRecorded()
    {
        ReleaseProfile profile = Infer(0x301, marks: 2, flags: true);

        Assert.Equal("3.3-3.32", profile.ToString());
        Assert.True(profile.RecordsStringEnds);
        Assert.True(profile.From(CompilerReleases.Hsp32));
        Assert.False(profile.From(CompilerReleases.Hsp331));
    }

    [Fact]
    public void LaterFormatWordTakesTheNearestKnownOne()
    {
        Assert.Equal("3.6-3.8β2 x64", Infer(0x370).ToString());
        Assert.Equal(CompilerReleases.All.Length, Infer(0x200).Candidates.Length);
    }

    [Fact]
    public void Hsp2FormatWordKeepsBothHsp2Releases()
    {
        ReleaseProfile profile = Infer(CompilerReleases.Word26);

        Assert.Equal("2.6-2.61", profile.ToString());
        Assert.False(profile.RedefinesBuiltins);
        Assert.True(profile.IncludesHspdef);
        Assert.Equal("2.61", profile.Pin("2.61")?.ToString());
        Assert.Null(profile.Pin("3.0"));
    }

    [Fact]
    public void RedefinedBuiltinLeavesOnly261()
    {
        var profile = ReleaseProfile.Infer(
            new AxEvidence(CompilerReleases.Word26, 0, false) { RedefinedBuiltin = true }
        );

        Assert.Equal("2.61", profile.ToString());
        Assert.True(profile.RedefinesBuiltins);
    }

    [Fact]
    public void RuntimeOfSourceWithCodeOnEveryLineLeavesItsDefaultWriter()
    {
        // Arrange
        var evidence = new AxEvidence(CompilerReleases.Word36, 0, false)
        {
            Runtime = CompilerReleases.Hsp64Runtime,
            BootOption = CompilerReleases.Hsp64BootOption,
            CodeOnEveryLine = true,
        };

        // Act
        var profile = ReleaseProfile.Infer(evidence);

        // Assert
        Assert.Equal("3.8β2 x64", profile.ToString());
    }

    [Fact]
    public void RuntimeOfSourceWithAFreeLineLeavesEveryRelease()
    {
        // Arrange
        var evidence = new AxEvidence(CompilerReleases.Word36, 0, false)
        {
            Runtime = CompilerReleases.Hsp64Runtime,
            BootOption = CompilerReleases.Hsp64BootOption,
        };

        // Act
        var profile = ReleaseProfile.Infer(evidence);

        // Assert
        Assert.Contains(CompilerReleases.Hsp38B2, profile.Candidates);
    }

    [Theory]
    [InlineData(CompilerReleases.Word25, "2.5", true)]
    [InlineData(CompilerReleases.Word255, "2.55", false)]
    public void EarlyHsp2WordNamesOneReleaseWithoutHspdef(
        uint word,
        string expected,
        bool byteValues
    )
    {
        ReleaseProfile profile = Infer(word);

        Assert.Equal(expected, profile.ToString());
        Assert.Equal(byteValues, profile.TokenLayout == Ax2TokenLayout.ByteValue);
        Assert.Equal(!byteValues, profile.LabelsRepeat);
        Assert.False(profile.IncludesHspdef);
    }

    [Fact]
    public void EvidenceThatRulesOutEveryCandidateIsIgnored()
    {
        ReleaseProfile profile = Infer(0x360, marks: 3);

        Assert.Equal("3.6-3.8β2 x64", profile.ToString());
        Assert.False(profile.MayWriteNormalBuildRecords);
    }

    [Fact]
    public void WideFramesNameTheX64Compiler()
    {
        ReleaseProfile profile = Infer(0x360, frames: FrameLayout.Wide);

        Assert.Equal("3.8β2 x64", profile.ToString());
        Assert.True(profile.WritesByDefault("hsp3_64", 0x43000));
        Assert.False(profile.WritesByDefault("hsp3_64", 0x3000));
        Assert.Equal(0x40000u, profile.DefaultBootOption);
    }

    [Fact]
    public void NarrowFramesKeepTheRuntimeLine()
    {
        ReleaseProfile profile = Infer(0x360, frames: FrameLayout.Narrow);

        Assert.Equal("3.6-3.8β2", profile.ToString());
        Assert.False(profile.WritesByDefault("hsp3_64", 0x43000));
        Assert.Equal(0u, profile.DefaultBootOption);
    }

    [Fact]
    public void WithoutFramesNoRuntimeIsDefault() =>
        Assert.False(Infer(0x360).WritesByDefault("hsp3_64", 0x43000));

    [Fact]
    public void HintPinsACandidateTheEvidenceLeaves()
    {
        ReleaseProfile profile = Infer(0x301, marks: 2);

        ReleaseProfile? pinned = profile.Pin("3.32");

        Assert.Equal("3.32", pinned?.ToString());
    }

    [Fact]
    public void HintNamesBetaReleasesWithoutTheGreekLetter()
    {
        ReleaseProfile profile = Infer(0x360);

        ReleaseProfile? pinned = profile.Pin("3.8B2");

        Assert.Equal("3.8β2", pinned?.ToString());
    }

    [Fact]
    public void HintTheEvidenceRulesOutIsRefused()
    {
        ReleaseProfile profile = Infer(0x301, marks: 2);

        ReleaseProfile? pinned = profile.Pin("3.4");

        Assert.Null(pinned);
    }

    [Fact]
    public void KeywordsRuleOutReleasesWithoutThem()
    {
        ReleaseSpan since34 = ReleaseSpan.Parse("3.4", null, ReleaseSpan.All)!.Value;

        var profile = ReleaseProfile.Infer(
            new AxEvidence(CompilerReleases.Word31, 0, false) { Keywords = since34 }
        );

        Assert.Equal("3.4", profile.ToString());
    }

    [Fact]
    public void KeywordsNoReleaseHasAreIgnored()
    {
        var profile = ReleaseProfile.Infer(
            new AxEvidence(CompilerReleases.Word31, 0, false) { Keywords = new ReleaseSpan(1, 0) }
        );

        Assert.Equal("3.1-3.4", profile.ToString());
    }

    [Fact]
    public void ReleasesAreInOrder()
    {
        Assert.Equal(
            Enumerable.Range(0, CompilerReleases.All.Length),
            CompilerReleases.All.Select(r => r.Order)
        );
    }
}
