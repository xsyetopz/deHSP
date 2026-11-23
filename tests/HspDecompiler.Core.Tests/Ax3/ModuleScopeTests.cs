using System.IO;
using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Ax3.Data.Line;
using HspDecompiler.Core.Ax3.Data.PP;
using Xunit;

namespace HspDecompiler.Core.Tests.Ax3;

public class ModuleScopeTests
{
    private static RenderContext Inside(string module, AxData? data = null) =>
        RenderContext.Of(data ?? new AxData()).In(module);

    private static Label LabelAt(int offset)
    {
        using var reader = new BinaryReader(new MemoryStream(System.BitConverter.GetBytes(offset)));
        return Label.FromBinaryReader(reader, new AxData(), 0);
    }

    [Theory]
    [InlineData("val@mymod", "val")]
    [InlineData("gcount", "gcount@")]
    [InlineData("other@lib", "other@lib")]
    public void VariableInsideModuleUsesItsScope(string recorded, string expected)
    {
        RenderContext context = Inside("mymod");

        Assert.Equal(expected, context.Variable(recorded));
    }

    [Fact]
    public void VariableKeepsSuffixWhenGlobalFunctionHoldsBareName()
    {
        var data = new AxData();
        data.Scope.AddGlobalFunction("show");

        Assert.Equal("show@a", Inside("a", data).Variable("show@a"));
    }

    [Fact]
    public void RendererEntersAtModuleAndLeavesAtGlobal()
    {
        var label = new PreprocessorDeclaration(LabelAt(0));
        var renderer = new LineRenderer(RenderContext.Of(new AxData()));

        string[] text =
        [
            renderer.Render(label),
            renderer.Render(new ModuleStart("mymod", false, null, 0)),
            renderer.Render(label),
            renderer.Render(new EndOfModule(0)),
            renderer.Render(label),
        ];

        Assert.Equal(["*label", "#module mymod", "*label@", "#global", "*label"], text);
    }

    [Fact]
    public void LocalFunctionDropsOwnSuffix()
    {
        RenderContext context = Inside("mymod");

        Assert.Equal("helper", context.Function("helper@mymod"));
        Assert.Equal("twice@lib", context.Function("twice@lib"));
    }

    [Fact]
    public void ImportInsideModuleTakesGlobalScope() =>
        Assert.Equal("GetTickCount@", Inside("mymod").Import("GetTickCount"));

    [Fact]
    public void ImportOutsideModuleStaysBare() =>
        Assert.Equal("GetTickCount", RenderContext.Of(new AxData()).Import("GetTickCount"));

    [Theory]
    [InlineData("skip", "mymod", "skip")]
    [InlineData("init", null, "init@")]
    [InlineData("inner", "helper", "inner@helper")]
    public void LabelInsideModuleUsesItsOwner(string name, string? owner, string expected) =>
        Assert.Equal(expected, Inside("mymod").Label(name, owner));
}
