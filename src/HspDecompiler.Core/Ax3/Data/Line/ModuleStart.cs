using HspDecompiler.Core.Ax3.Data.PP;

namespace HspDecompiler.Core.Ax3.Data.Line;

// The #module line that hspcmp expanded to "goto *_name_exit". It takes the goto's code
// offset, so a debug layout puts it on the goto's line, which is the #module line. A module
// with members writes them here instead of in a #struct line.
//
// hspcmp が "goto *_名前_exit" に展開した #module 行。goto のコード位置を引き継ぐため、
// デバッグ情報の配置では goto の行、つまり #module の行に置かれる。メンバーを持つモジュールは
// #struct 行ではなくここにメンバーを書く。
internal sealed class ModuleStart : LogicalLine
{
    private readonly int _tokenOffset;
    private readonly bool _anonymous;
    private readonly Function? _members;

    internal ModuleStart(string name, bool anonymous, Function? members, int tokenOffset)
    {
        Name = name;
        _anonymous = anonymous;
        _members = members;
        _tokenOffset = tokenOffset;
    }

    internal string Name { get; }

    internal override int TokenOffset => _tokenOffset;

    internal override int TabCount => 0;

    public override string ToString() =>
        _members != null ? _members.ToString(true)
        : _anonymous ? "#module"
        : "#module " + Name;
}
