using System;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using HspDecompiler.Core.Ax3;
using HspDecompiler.Core.Ax3.Dictionary;
using HspDecompiler.Core.Pipeline;
using HspDecompiler.Core.Tests.IntegrationTests;
using Xunit;

namespace HspDecompiler.Core.Tests;

public sealed class Hsp3DictionaryTests : IDisposable
{
    private const string Header =
        "<dictionary xmlns=\"urn:dehsp:dictionary\" version=\"test\">\n<codes>\n";

    private const string Footer =
        "</codes>\n<paramTypes><paramType code=\"0x0004\" name=\"int\" /></paramTypes>\n</dictionary>";

    private readonly string _path = Path.GetTempFileName();

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void BuiltInLoadsHsp38Beta2Table()
    {
        Hsp3Dictionary dict = Hsp3Dictionary.BuiltIn;

        Assert.Equal("3.8beta2", dict.Version);
        Assert.True(
            dict.CodeLookUp(new HspDictionaryKey(0x0F, 0x001E), out HspDictionaryValue varprop)
        );
        Assert.Equal("varprop", varprop._name);
        Assert.True(
            dict.CodeLookUp(new HspDictionaryKey(0x00, 0x0002), out HspDictionaryValue mul)
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
            Hsp3Dictionary.BuiltIn.CodeLookUp(
                new HspDictionaryKey(0x00, 0x0005),
                out HspDictionaryValue and
            )
        );
        Assert.Equal("&", and._name);
    }

    [Fact]
    public void BuiltInWildcardEntryMatchesEveryValue()
    {
        Assert.True(
            Hsp3Dictionary.BuiltIn.CodeLookUp(
                new HspDictionaryKey(0x01, 1234),
                out HspDictionaryValue variable
            )
        );
        Assert.Equal(HspCodeType.Variable, variable._type);
    }

    [Fact]
    public void BuiltInParsesCombinedFlags()
    {
        Assert.True(
            Hsp3Dictionary.BuiltIn.CodeLookUp(
                new HspDictionaryKey(0x0F, 0x0004),
                out HspDictionaryValue repeat
            )
        );
        Assert.Equal(HspCodeExtraOptions.HasGhostLabel | HspCodeExtraOptions.AddTab, repeat._extra);
    }

    [Fact]
    public void FromFileReadsCustomDictionary()
    {
        WriteCodes("<code type=\"0x09\" value=\"0x0001\" name=\"custom\" kind=\"HspFunction\" />");

        var dict = Hsp3Dictionary.FromFile(_path);

        Assert.Equal("test", dict.Version);
        Assert.True(dict.CodeLookUp(new HspDictionaryKey(0x09, 0x0001), out HspDictionaryValue v));
        Assert.Equal("custom", v._name);
    }

    [Fact]
    public void FromFileMissingFileThrows() =>
        Assert.ThrowsAny<IOException>(() =>
            Hsp3Dictionary.FromFile("/nonexistent/path/Dictionary.xml")
        );

    [Fact]
    public void FromFileUnknownKindFailsSchemaWithLine()
    {
        WriteCodes("<code type=\"0x01\" kind=\"NoSuchKind\" />");

        XmlSchemaException ex = Assert.ThrowsAny<XmlSchemaException>(() =>
            Hsp3Dictionary.FromFile(_path)
        );

        Assert.Equal(3, ex.LineNumber);
    }

    [Fact]
    public void FromFileUnpaddedHexFailsSchema()
    {
        WriteCodes("<code type=\"0x9\" value=\"0x1\" kind=\"HspFunction\" />");

        Assert.ThrowsAny<XmlSchemaException>(() => Hsp3Dictionary.FromFile(_path));
    }

    [Fact]
    public void FromFileDuplicateKeyFailsSchema()
    {
        WriteCodes(
            "<code type=\"0x09\" value=\"0x0001\" name=\"a\" kind=\"HspFunction\" />\n"
                + "<code type=\"0x09\" value=\"0x0001\" name=\"b\" kind=\"HspFunction\" />"
        );

        Assert.ThrowsAny<XmlSchemaException>(() => Hsp3Dictionary.FromFile(_path));
    }

    [Fact]
    public void FromFileDuplicateWildcardThrowsWithLine()
    {
        WriteCodes(
            "<code type=\"0x01\" kind=\"Variable\" />\n<code type=\"0x01\" kind=\"Variable\" />"
        );

        InvalidDataException ex = Assert.Throws<InvalidDataException>(() =>
            Hsp3Dictionary.FromFile(_path)
        );

        Assert.Contains("4", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromFileRootOutsideNamespaceFails()
    {
        File.WriteAllText(_path, "<dictionary version=\"x\"><codes /><paramTypes /></dictionary>");

        Assert.ThrowsAny<XmlSchemaException>(() => Hsp3Dictionary.FromFile(_path));
    }

    [Fact]
    public void FromFileRejectsDtd()
    {
        File.WriteAllText(_path, "<!DOCTYPE d [<!ENTITY e \"x\">]>" + Header + Footer);

        Assert.Throws<XmlException>(() => Hsp3Dictionary.FromFile(_path));
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
