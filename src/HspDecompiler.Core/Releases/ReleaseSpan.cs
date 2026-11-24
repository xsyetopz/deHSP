using System;
using System.Linq;

namespace HspDecompiler.Core.Releases;

// A run of consecutive releases, by CompilerRelease.Order, such as the releases whose hspcmp
// knows a keyword. Both ends are inclusive.
//
// CompilerRelease.Order で数えた連続するリリースの範囲。キーワードを知る hspcmp のリリース
// などを表す。両端を含む。
internal readonly record struct ReleaseSpan(int First, int Last)
{
    internal static ReleaseSpan All { get; } = new(0, CompilerReleases.All.Length - 1);

    // The releases of one major version, such as every HSP 3 release; empty for a major the
    // table lacks.
    //
    // 1 つのメジャーバージョンのリリース。例えば HSP 3 のすべてのリリース。表に無いメジャー
    // バージョンでは空。
    internal static ReleaseSpan OfMajor(uint major)
    {
        CompilerRelease[] releases = [.. CompilerReleases.All.Where(r => r.Major == major)];
        return releases.Length == 0
            ? new ReleaseSpan(1, 0)
            : new ReleaseSpan(releases[0].Order, releases[^1].Order);
    }

    internal bool IsEmpty => First > Last;

    internal bool Contains(CompilerRelease release) =>
        release.Order >= First && release.Order <= Last;

    internal bool Overlaps(ReleaseSpan other) => First <= other.Last && other.First <= Last;

    internal bool Overlaps(ReleaseProfile profile) => profile.Candidates.Any(Contains);

    // The releases both spans hold; an empty result keeps First above Last.
    //
    // 両方の範囲が持つリリース。空の時は First が Last より大きくなる。
    internal ReleaseSpan Intersect(ReleaseSpan other) =>
        new(Math.Max(First, other.First), Math.Min(Last, other.Last));

    // The smallest span holding both, which also covers any gap between them.
    //
    // 両方を含む最小の範囲。間に隙間があればそれも含む。
    internal ReleaseSpan Hull(ReleaseSpan other) =>
        new(Math.Min(First, other.First), Math.Max(Last, other.Last));

    // The span a dictionary entry's since and until attributes name; null when either names no
    // release in the table.
    //
    // 辞書の項目の since と until が指す範囲。どちらかが表に無いリリースを指す時は null。
    internal static ReleaseSpan? Parse(string? since, string? until, ReleaseSpan outer)
    {
        int first = outer.First;
        int last = outer.Last;
        if (since != null)
        {
            if (Find(since) is not { } release)
            {
                return null;
            }

            first = release.Order;
        }

        if (until != null)
        {
            if (Find(until) is not { } release)
            {
                return null;
            }

            last = release.Order;
        }

        return new ReleaseSpan(first, last);
    }

    private static CompilerRelease? Find(string name) =>
        CompilerReleases.All.FirstOrDefault(r => r.Names(name));

    public override string ToString() =>
        IsEmpty ? "?" : CompilerReleases.All[First].Name + "-" + CompilerReleases.All[Last].Name;
}
