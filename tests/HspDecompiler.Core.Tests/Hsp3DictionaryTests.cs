using System.IO;
using HspDecompiler.Core.Ax3;
using HspDecompiler.Core.Ax3.Dictionary;
using Xunit;

namespace HspDecompiler.Core.Tests;

public class Hsp3DictionaryTests
{
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
    public void FromFileMissingFileReturnsNull() =>
        Assert.Null(Hsp3Dictionary.FromFile("/nonexistent/path/Dictionary.json"));

    [Fact]
    public void FromFileMalformedJsonReturnsNull()
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, "{ \"codes\": [ { \"type\": 1, \"kind\": \"NoSuchKind\" } ] }");
        try
        {
            Assert.Null(Hsp3Dictionary.FromFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FromFileDuplicateKeyReturnsNull()
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(
            path,
            "{ \"codes\": [ { \"type\": 9, \"value\": 1, \"name\": \"a\", \"kind\": \"HspFunction\" }, { \"type\": 9, \"value\": 1, \"name\": \"b\", \"kind\": \"HspFunction\" } ] }"
        );
        try
        {
            Assert.Null(Hsp3Dictionary.FromFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
