using HspDecompiler.Core.Ax3.Data.Layout;
using HspDecompiler.Core.Ax3.Data.Line;
using Xunit;

namespace HspDecompiler.Core.Tests.Ax3;

public class LineKindTests
{
    [Fact]
    public void BlankCommentIsBlankAndWrittenCommentIsDirective()
    {
        Assert.Equal(LineKind.Blank, LineKinds.Of(new CommentLine()));
        Assert.Equal(LineKind.Directive, LineKinds.Of(new CommentLine(string.Empty)));
    }

    [Fact]
    public void ModuleLinesKeepTheirOwnKinds()
    {
        Assert.Equal(LineKind.ModuleStart, LineKinds.Of(new ModuleStart("m", false, null, -1)));
        Assert.Equal(LineKind.ModuleEnd, LineKinds.Of(new EndOfModule(-1)));
    }

    [Fact]
    public void OnlyCodeAndBracesTakeAJoinAndLabelsAlsoLetOne()
    {
        foreach (LineKind kind in System.Enum.GetValues<LineKind>())
        {
            Assert.Equal(kind is LineKind.Code or LineKind.Close, kind.TakesJoin());
            Assert.Equal(
                kind is LineKind.Code or LineKind.Close or LineKind.Label,
                kind.LetsJoin()
            );
        }
    }
}
