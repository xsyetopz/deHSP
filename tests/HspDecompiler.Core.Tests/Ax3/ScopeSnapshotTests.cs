using System.IO;
using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Ax3.Data.Layout;
using HspDecompiler.Core.Ax3.Data.PP;
using Xunit;

namespace HspDecompiler.Core.Tests.Ax3;

public class ScopeSnapshotTests
{
    private static Label LabelAt(int offset)
    {
        using var reader = new BinaryReader(new MemoryStream(System.BitConverter.GetBytes(offset)));
        return Label.FromBinaryReader(reader, new AxData(), 0);
    }

    [Fact]
    public void RestoreGivesBackTheLabelModulesAndGlobalFunctions()
    {
        var data = new AxData();
        Label label = LabelAt(4);
        data.Labels.Add(label);
        var before = ScopeSnapshot.Take(data);
        label.Owner = "m";
        data.Scope.AddGlobalFunction("f");

        before.Restore(data);

        Assert.Null(label.Owner);
        Assert.Empty(data.Scope.GlobalFunctions);
        Assert.Equal("*label", label.LabelName);
    }
}
