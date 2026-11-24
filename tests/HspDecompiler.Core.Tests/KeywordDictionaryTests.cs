using System;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using HspDecompiler.Core.Ax3;
using HspDecompiler.Core.Keywords;
using HspDecompiler.Core.Pipeline;
using HspDecompiler.Core.Releases;
using HspDecompiler.Core.Tests.IntegrationTests;
using Xunit;

namespace HspDecompiler.Core.Tests;

public sealed class KeywordDictionaryTests : IDisposable
{
    private const string Header =
        "<dictionary xmlns=\"urn:dehsp:dictionary\" version=\"test\">\n<codes>\n";

    private const string Footer =
        "</codes>\n<paramTypes><paramType code=\"0x0004\" name=\"int\" /></paramTypes>\n</dictionary>";

    private static readonly ReleaseProfile s_latest = ReleaseProfile.ForFormat(
        CompilerReleases.Word36
    );

    private readonly string _path = Path.GetTempFileName();

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void BuiltInLoadsEveryReleaseTable()
    {
        KeywordDictionary dict = KeywordDictionary.BuiltIn;

        Assert.Equal("2.4-3.8beta2", dict.Version);
        Assert.True(
            dict.CodeLookUp(
                new HspDictionaryKey(0x0F, 0x001E),
                s_latest,
                out HspDictionaryValue varprop
            )
        );
        Assert.Equal("varprop", varprop._name);
        Assert.True(
            dict.CodeLookUp(
                new HspDictionaryKey(0x00, 0x0002),
                s_latest,
                out HspDictionaryValue mul
            )
        );
        Assert.Equal(HspCodeType.Operator, mul._type);
        Assert.Equal(4, mul._operatorPriority);
        Assert.True(dict.ParamLookUp(0xFFFA, out string? str));
        Assert.Equal("str", str);
    }

    [Fact]
    public void BuiltInKeepsEscapedOperatorNames()
    {
        Assert.True(
            KeywordDictionary.BuiltIn.CodeLookUp(
                new HspDictionaryKey(0x00, 0x0005),
                s_latest,
                out HspDictionaryValue and
            )
        );
        Assert.Equal("&", and._name);
    }

    [Fact]
    public void BuiltInWildcardEntryMatchesEveryValue()
    {
        Assert.True(
            KeywordDictionary.BuiltIn.CodeLookUp(
                new HspDictionaryKey(0x01, 1234),
                s_latest,
                out HspDictionaryValue variable
            )
        );
        Assert.Equal(HspCodeType.Variable, variable._type);
    }

    [Fact]
    public void BuiltInParsesCombinedFlags()
    {
        Assert.True(
            KeywordDictionary.BuiltIn.CodeLookUp(
                new HspDictionaryKey(0x0F, 0x0004),
                s_latest,
                out HspDictionaryValue repeat
            )
        );
        Assert.Equal(HspCodeExtraOptions.HasGhostLabel | HspCodeExtraOptions.AddTab, repeat._extra);
    }

    [Fact]
    public void FromFileReadsCustomDictionary()
    {
        WriteCodes("<code type=\"0x09\" value=\"0x0001\" name=\"custom\" kind=\"HspFunction\" />");

        var dict = KeywordDictionary.FromFile(_path);

        Assert.Equal("test", dict.Version);
        Assert.True(
            dict.CodeLookUp(new HspDictionaryKey(0x09, 0x0001), s_latest, out HspDictionaryValue v)
        );
        Assert.Equal("custom", v._name);
    }

    [Fact]
    public void FromFileMissingFileThrows() =>
        Assert.ThrowsAny<IOException>(() =>
            KeywordDictionary.FromFile("/nonexistent/path/Dictionary.xml")
        );

    [Fact]
    public void FromFileUnknownKindFailsSchemaWithLine()
    {
        WriteCodes("<code type=\"0x01\" kind=\"NoSuchKind\" />");

        XmlSchemaException ex = Assert.ThrowsAny<XmlSchemaException>(() =>
            KeywordDictionary.FromFile(_path)
        );

        Assert.Equal(3, ex.LineNumber);
    }

    [Fact]
    public void FromFileUnpaddedHexFailsSchema()
    {
        WriteCodes("<code type=\"0x9\" value=\"0x1\" kind=\"HspFunction\" />");

        Assert.ThrowsAny<XmlSchemaException>(() => KeywordDictionary.FromFile(_path));
    }

    [Fact]
    public void FromFileOverlappingVariantsThrowWithLine()
    {
        WriteCodes(
            "<code type=\"0x09\" value=\"0x0001\" name=\"a\" kind=\"HspFunction\" until=\"3.4\" />\n"
                + "<code type=\"0x09\" value=\"0x0001\" name=\"b\" kind=\"HspFunction\" since=\"3.4\" />"
        );

        InvalidDataException ex = Assert.Throws<InvalidDataException>(() =>
            KeywordDictionary.FromFile(_path)
        );

        Assert.Contains("4", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromFileVariantsOfAnotherKindThrow()
    {
        WriteCodes(
            "<code type=\"0x09\" value=\"0x0001\" name=\"a\" kind=\"HspFunction\" until=\"3.4\" />\n"
                + "<code type=\"0x09\" value=\"0x0001\" name=\"b\" kind=\"Variable\" since=\"3.5\" />"
        );

        Assert.Throws<InvalidDataException>(() => KeywordDictionary.FromFile(_path));
    }

    [Fact]
    public void FromFileUnknownReleaseThrows()
    {
        WriteCodes(
            "<code type=\"0x09\" value=\"0x0001\" name=\"a\" kind=\"HspFunction\" since=\"9.9\" />"
        );

        InvalidDataException ex = Assert.Throws<InvalidDataException>(() =>
            KeywordDictionary.FromFile(_path)
        );

        Assert.Contains("9.9", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromFileSectionSpanIsTheEntriesDefault()
    {
        File.WriteAllText(
            _path,
            "<dictionary xmlns=\"urn:dehsp:dictionary\" version=\"test\">\n<codes since=\"3.5\">\n"
                + "<code type=\"0x09\" value=\"0x0001\" name=\"a\" kind=\"HspFunction\" />\n"
                + "<code type=\"0x09\" value=\"0x0002\" name=\"b\" kind=\"HspFunction\" until=\"3.6\" />\n"
                + Footer
        );

        var dict = KeywordDictionary.FromFile(_path);

        Assert.Equal("3.5-3.8β2 x64", dict.SpanOf(0x09, 0x0001).ToString());
        Assert.Equal("3.5-3.6", dict.SpanOf(0x09, 0x0002).ToString());
    }

    [Theory]
    [InlineData("3.6", "resume")]
    [InlineData("3.7", "varprop")]
    public void VariantNamesFollowTheRelease(string release, string name)
    {
        ReleaseProfile profile = s_latest.Pin(release)!;

        Assert.Equal(name, KeywordDictionary.BuiltIn.NameOf(0x0F, 0x1E, profile));
        Assert.True(KeywordDictionary.BuiltIn.HasVariants(0x0F, 0x1E));
    }

    [Fact]
    public void AmbiguousVariantTakesTheNewestCandidate() =>
        Assert.Equal("varprop", KeywordDictionary.BuiltIn.NameOf(0x0F, 0x1E, s_latest));

    [Fact]
    public void LaterCodeStillReadsUnderAnEarlierRelease()
    {
        var hsp30 = ReleaseProfile.ForFormat(CompilerReleases.Word30);

        Assert.Equal("int64", KeywordDictionary.BuiltIn.NameOf(0x0D, 0x0200, hsp30));
        Assert.Equal("strrep", KeywordDictionary.BuiltIn.NameOf(0x08, 0x002B, hsp30));
    }

    [Fact]
    public void Hsp2CodesAreNamedOnlyForHsp2()
    {
        var hsp2 = ReleaseProfile.ForFormat(CompilerReleases.Word26);

        Assert.Equal("system", KeywordDictionary.BuiltIn.NameOf(0x38, 0x00, hsp2));
        Assert.Null(KeywordDictionary.BuiltIn.NameOf(0x38, 0x00, s_latest));
        Assert.True(KeywordDictionary.BuiltIn.IsName("csrx", hsp2));
        Assert.False(KeywordDictionary.BuiltIn.IsName("csrx", s_latest));
    }

    [Theory]
    [InlineData(CompilerReleases.Word255, 0x40, 0x08, "on")]
    [InlineData(CompilerReleases.Word26, 0x40, 0x08, "onerror")]
    [InlineData(CompilerReleases.Word26, 0x40, 0x09, "on")]
    [InlineData(CompilerReleases.Word25, 0x48, 0x43, "notesel")]
    [InlineData(CompilerReleases.Word26, 0x48, 0x43, "sysinfo")]
    [InlineData(CompilerReleases.Word24, 0x48, 0x42, "hspmode")]
    [InlineData(CompilerReleases.Word25, 0x48, 0x42, "objmode")]
    [InlineData(CompilerReleases.Word24, 0x48, 0x43, "notesel")]
    public void Hsp2CodeTakesItsReleasesName(uint word, int type, int value, string expected) =>
        Assert.Equal(
            expected,
            KeywordDictionary.BuiltIn.NameOf(type, value, ReleaseProfile.ForFormat(word))
        );

    [Fact]
    public void MacroNeedsEveryCandidateToHaveIt()
    {
        var hsp31 = ReleaseProfile.ForFormat(CompilerReleases.Word31);

        Assert.Null(KeywordDictionary.BuiltIn.MacroFor("dirinfo", 5, hsp31));
        Assert.Equal("dir_tv", KeywordDictionary.BuiltIn.MacroFor("dirinfo", 5, hsp31.Pin("3.4")!));
        Assert.Equal("dir_exe", KeywordDictionary.BuiltIn.MacroFor("dirinfo", 1, hsp31));
    }

    [Fact]
    public void FromFileDuplicateWildcardThrowsWithLine()
    {
        WriteCodes(
            "<code type=\"0x01\" kind=\"Variable\" />\n<code type=\"0x01\" kind=\"Variable\" />"
        );

        InvalidDataException ex = Assert.Throws<InvalidDataException>(() =>
            KeywordDictionary.FromFile(_path)
        );

        Assert.Contains("4", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromFileRootOutsideNamespaceFails()
    {
        File.WriteAllText(_path, "<dictionary version=\"x\"><codes /><paramTypes /></dictionary>");

        Assert.ThrowsAny<XmlSchemaException>(() => KeywordDictionary.FromFile(_path));
    }

    [Fact]
    public void FromFileRejectsDtd()
    {
        File.WriteAllText(_path, "<!DOCTYPE d [<!ENTITY e \"x\">]>" + Header + Footer);

        Assert.Throws<XmlException>(() => KeywordDictionary.FromFile(_path));
    }

    [Fact]
    public void PipelineLogsWhyCustomDictionaryFailed()
    {
        WriteCodes("<code type=\"0x01\" kind=\"NoSuchKind\" />");
        var logger = new NullLogger();
        var pipeline = new DecompilerPipeline(logger, new NullProgressReporter());

        bool loaded = pipeline.Initialize(_path);

        Assert.False(loaded);
        Assert.Contains(logger.Messages, m => m.Contains("NoSuchKind", StringComparison.Ordinal));
    }

    private void WriteCodes(string codes) =>
        File.WriteAllText(_path, Header + codes + "\n" + Footer);
}
