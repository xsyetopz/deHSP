using HspDecompiler.Core.Ax3.Data.Layout;
using Xunit;

namespace HspDecompiler.Core.Tests.Ax3;

public class CommonHeaderTests
{
    private const string Header = """
        #uselib "user32.dll"
        #ifdef __hsp64__
        #func global MessageBoxA "MessageBoxA" int,sptr,sptr,int ; 64-bit
        #else
        #func global MessageBoxA "MessageBoxA" $202
        #endif
        /* #define Hidden 1 */
        #define WM_USER 0x400
        """;

    [Fact]
    public void ReadsDllsAndDefinedNames()
    {
        var header = CommonHeader.Parse(Header);

        Assert.Contains("USER32.DLL", header.Dlls);
        Assert.Contains("messageboxa", header.Names);
        Assert.Contains("wm_user", header.Names);
        Assert.DoesNotContain("hidden", header.Names);
    }

    [Fact]
    public void ReadsBothBranchesOfIfdef()
    {
        var header = CommonHeader.Parse(Header);

        Assert.Equal(2, header.Imports.Count);
        Assert.All(header.Imports, import => Assert.Equal("MessageBoxA", import.Export));
    }

    [Fact]
    public void OldTypeCodeMatchesDecimalSpelling()
    {
        ImportDeclaration? hex = CommonHeader.ParseImport("#func f \"F\" $202");
        ImportDeclaration? dec = CommonHeader.ParseImport("#func g \"F\" 514");

        Assert.Equal(hex!.Signature, dec!.Signature);
        Assert.Equal("f", hex.Alias);
    }
}
