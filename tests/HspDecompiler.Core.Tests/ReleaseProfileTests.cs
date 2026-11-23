using System.Linq;
using HspDecompiler.Core.Releases;
using Xunit;

namespace HspDecompiler.Core.Tests;

public class ReleaseProfileTests
{
    private static ReleaseProfile Infer(uint word, int marks = 0, bool flags = false) =>
        ReleaseProfile.Infer(new AxEvidence(word, marks, flags));

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
        Assert.Equal("3.6-3.8β2", Infer(0x360).ToString());
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
        Assert.Equal("3.6-3.8β2", Infer(0x370).ToString());
        Assert.Equal(CompilerReleases.All.Length, Infer(0x200).Candidates.Length);
    }

    [Fact]
    public void EvidenceThatRulesOutEveryCandidateIsIgnored()
    {
        ReleaseProfile profile = Infer(0x360, marks: 3);

        Assert.Equal("3.6-3.8β2", profile.ToString());
        Assert.False(profile.MayWriteNormalBuildRecords);
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
