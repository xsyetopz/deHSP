using System.Linq;
using HspDecompiler.Core.Ax3.Data.PP;

namespace HspDecompiler.Core.Ax3.Data.Layout;

// The module each label is spelled in and the global functions the scope knows, taken before a
// layout rescopes them, so a layout that fails leaves the data as it found it.
//
// 各ラベルを書くモジュールとスコープが知るグローバル関数。配置がそれらを組み替える前に取り、
// 失敗した配置がデータを元のままに残せるようにする。
internal sealed class ScopeSnapshot
{
    private readonly (Label Label, string? Owner)[] _labels;
    private readonly string[] _globalFunctions;

    private ScopeSnapshot((Label, string?)[] labels, string[] globalFunctions)
    {
        _labels = labels;
        _globalFunctions = globalFunctions;
    }

    internal static ScopeSnapshot Take(AxData data) =>
        new(
            [.. data.Labels.Select(label => (label, label.Owner))],
            [.. data.Scope.GlobalFunctions]
        );

    internal void Restore(AxData data)
    {
        foreach ((Label label, string? owner) in _labels)
        {
            label.Owner = owner;
        }

        data.Scope.Reset(_globalFunctions);
    }
}
