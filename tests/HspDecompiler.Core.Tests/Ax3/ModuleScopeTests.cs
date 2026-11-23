using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Ax3.Data.Line;
using Xunit;

namespace HspDecompiler.Core.Tests.Ax3;

public class ModuleScopeTests
{
    private static ModuleScope Inside(string module)
    {
        var scope = new ModuleScope();
        _ = scope.Render(new ModuleStart(module, false, null, 0));
        return scope;
    }

    [Theory]
    [InlineData("val@mymod", "val")]
    [InlineData("gcount", "gcount@")]
    [InlineData("other@lib", "other@lib")]
    public void VariableInsideModuleUsesItsScope(string recorded, string expected)
    {
        ModuleScope scope = Inside("mymod");

        Assert.Equal(expected, scope.Variable(recorded));
    }

    [Fact]
    public void VariableKeepsSuffixWhenGlobalFunctionHoldsBareName()
    {
        ModuleScope scope = Inside("a");
        scope.AddGlobalFunction("show");

        Assert.Equal("show@a", scope.Variable("show@a"));
    }

    [Fact]
    public void GlobalLeavesModuleScope()
    {
        ModuleScope scope = Inside("mymod");

        _ = scope.Render(new EndOfModule(0));

        Assert.Equal("val@mymod", scope.Variable("val@mymod"));
        Assert.Equal("gcount", scope.Variable("gcount"));
    }

    [Fact]
    public void LocalFunctionDropsOwnSuffix()
    {
        ModuleScope scope = Inside("mymod");

        Assert.Equal("helper", scope.Function("helper@mymod"));
        Assert.Equal("twice@lib", scope.Function("twice@lib"));
    }

    [Theory]
    [InlineData("skip", "mymod", "skip")]
    [InlineData("init", null, "init@")]
    [InlineData("inner", "helper", "inner@helper")]
    public void LabelInsideModuleUsesItsOwner(string name, string? owner, string expected)
    {
        ModuleScope scope = Inside("mymod");

        Assert.Equal(expected, scope.Label(name, owner));
    }
}
