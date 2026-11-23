using HspDecompiler.Core.Ax3;
using HspDecompiler.Core.Releases;
using Xunit;

namespace HspDecompiler.Core.Tests;

public class DirectivePlannerTests
{
    private static DirectiveFacts Facts(
        uint word,
        bool repeated = false,
        bool uncalled = false,
        bool unusedModule = false,
        bool names = false,
        bool debug = false,
        bool flags = true
    ) =>
        new(
            ReleaseProfile.Infer(new AxEvidence(word, 0, flags)),
            repeated,
            uncalled,
            unusedModule,
            names,
            debug,
            flags,
            0,
            false,
            false,
            null
        );

    [Theory]
    [InlineData(0x300u, false)]
    [InlineData(0x301u, true)]
    [InlineData(0x360u, true)]
    public void UncalledImportTurnsOptcodeOffWhereEveryCandidateDropsIt(uint word, bool expected) =>
        Assert.Equal(
            expected,
            DirectivePlanner
                .Plan(Facts(word, uncalled: true))
                .Contains(DirectivePlanner.KeepUnusedImportsDirective)
        );

    [Theory]
    [InlineData(0x350u, false)]
    [InlineData(0x360u, true)]
    public void RepeatedStringTurnsOptcodeOffWhereEveryCandidatePoolsStrings(
        uint word,
        bool expected
    ) =>
        Assert.Equal(
            expected,
            DirectivePlanner
                .Plan(Facts(word, repeated: true))
                .Contains(DirectivePlanner.KeepUnusedImportsDirective)
        );

    [Fact]
    public void UnusedModuleTurnsOptcodeOff() =>
        Assert.Equal(
            [DirectivePlanner.KeepUnusedImportsDirective],
            DirectivePlanner.Plan(Facts(0x360, unusedModule: true))
        );

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void VariableNamesAskForVarnameOnlyWithoutLineRecords(bool debug, bool expected) =>
        Assert.Equal(
            expected,
            DirectivePlanner
                .Plan(Facts(0x360, names: true, debug: debug))
                .Contains(DirectivePlanner.KeepVariableNamesDirective)
        );

    [Theory]
    [InlineData(0x301u, false)]
    [InlineData(0x350u, true)]
    public void MissingOptprmFlagsAskForOptprmOffWhereEveryCandidateKnowsIt(
        uint word,
        bool expected
    ) =>
        Assert.Equal(
            expected,
            DirectivePlanner
                .Plan(Facts(word, flags: false))
                .Contains(DirectivePlanner.PlainParametersDirective)
        );

    [Fact]
    public void PlainAxNeedsNoDirective() => Assert.Empty(DirectivePlanner.Plan(Facts(0x360)));
}
