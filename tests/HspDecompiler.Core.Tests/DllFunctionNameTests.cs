using System.Collections.Generic;
using HspDecompiler.Core.Ax3.Naming;
using Xunit;

namespace HspDecompiler.Core.Tests;

public class DllFunctionNameTests
{
    [Fact]
    public void StripsStdcallDecorationWhenNameIsFree()
    {
        var taken = new HashSet<string>();
        Assert.Equal("OpenProject", NameAssigner.ResolveDllFunctionName("_OpenProject@8", taken));
    }

    [Fact]
    public void KeepsUndecoratedExportWithoutPrefix()
    {
        var taken = new HashSet<string>();
        Assert.Equal("GetTickCount", NameAssigner.ResolveDllFunctionName("GetTickCount", taken));
    }

    [Fact]
    public void FallsBackToUnderscoreExportOnCaseInsensitiveCollision()
    {
        // A wrapper "#deffunc openproject" collides with the bare import name.
        //
        // ラッパーの "#deffunc openproject" が素のインポート名と衝突する。
        var taken = new HashSet<string> { "openproject" };
        Assert.Equal("_OpenProject", NameAssigner.ResolveDllFunctionName("_OpenProject@8", taken));
    }

    [Fact]
    public void FallsBackToDllSuffixWhenUnderscoreFormIsTaken()
    {
        var taken = new HashSet<string> { "openproject", "_openproject" };
        Assert.Equal(
            "OpenProject_dll",
            NameAssigner.ResolveDllFunctionName("_OpenProject@8", taken)
        );
    }

    [Fact]
    public void UsesDllSuffixForUnprefixedExportOnCollision()
    {
        var taken = new HashSet<string> { "closehandle" };
        Assert.Equal("CloseHandle_dll", NameAssigner.ResolveDllFunctionName("CloseHandle", taken));
    }

    [Fact]
    public void NumbersRepeatedImportsOfTheSameExport()
    {
        var taken = new HashSet<string> { "closehandle", "closehandle_dll" };
        Assert.Equal("CloseHandle_dll2", NameAssigner.ResolveDllFunctionName("CloseHandle", taken));
    }

    [Fact]
    public void FallsBackToNumberedNameWhenAllCandidatesAreTaken()
    {
        var taken = new HashSet<string> { "closehandle", "closehandle_dll", "func_1" };
        for (int n = 2; n <= 9; n++)
        {
            taken.Add($"closehandle_dll{n}");
        }
        Assert.Equal("func_2", NameAssigner.ResolveDllFunctionName("CloseHandle", taken));
    }

    [Theory]
    [InlineData("?Fn@@YAXH@Z")]
    [InlineData("#12")]
    [InlineData("")]
    public void RejectsExportsThatAreNotHspIdentifiers(string exportName)
    {
        var taken = new HashSet<string>();
        Assert.Equal("func_1", NameAssigner.ResolveDllFunctionName(exportName, taken));
    }
}
