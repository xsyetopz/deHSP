using System;
using System.Collections.Immutable;
using System.Linq;

namespace HspDecompiler.Core.Releases;

// What one AX shows about the compiler that wrote it, gathered once after the code is read.
// FormatWord is the header version. NormalBuildMarks counts the line records of a build without
// -d, SingleItemFlags says some one-literal parameter carries hspcmp's optprm flag, and Frames is
// the parameter layout a user function shows, if any does.
//
// 1 つの AX が書いたコンパイラについて示すこと。コードを読んだ後に一度だけ集める。FormatWord は
// ヘッダーのバージョン、NormalBuildMarks は -d 無しのビルドの行の記録の数、SingleItemFlags は
// 1 リテラルの引数のどれかに hspcmp の optprm の印があること、Frames はユーザー関数が示す引数の
// 並べ方(示すものがあれば)。
internal sealed record AxEvidence(
    uint FormatWord,
    int NormalBuildMarks,
    bool SingleItemFlags,
    FrameLayout? Frames = null
);

// The releases that could have written one AX, and the questions the decompiler's stages ask of
// them. Several releases share a format word, so a stage asks whether every candidate does a
// thing before it relies on it; then the output recompiles the same on each of them. Each
// question names its quantifier once, here.
//
// 1 つの AX を書いた可能性のあるリリースと、デコンパイラの各段がそれに尋ねる問い。複数の
// リリースが形式のワードを共有するため、段はすべての候補がそれをするかを確かめてから頼る。
// そうすれば出力はどの候補でも同じに再コンパイルされる。各問いの量化はここで一度だけ決める。
internal sealed class ReleaseProfile
{
    internal ReleaseProfile(ImmutableArray<CompilerRelease> candidates)
    {
        Candidates = candidates;
    }

    // Every release, for an AX the decompiler knows nothing about yet.
    //
    // まだ何も分かっていない AX のための全リリース。
    internal static ReleaseProfile Unknown { get; } = new(CompilerReleases.All);

    internal ImmutableArray<CompilerRelease> Candidates { get; }

    internal bool DropsUnused => All(r => r.Optcode != OptcodeMode.None);

    internal bool PoolsStrings => All(r => r.Optcode == OptcodeMode.DropsUnusedAndPoolsStrings);

    internal bool KnowsOptprm => All(r => r.Optprm == OptprmSupport.Known);

    internal bool MayWriteNormalBuildRecords =>
        Candidates.Any(r => r.Records != NormalBuildRecords.None);

    internal bool RecordsStringEnds =>
        All(r => r.Records == NormalBuildRecords.ModulesAndStringEnds);

    // True when every candidate writes this runtime and these boot bits for a source that names
    // no runtime, so the source needs neither the #runtime line nor the #bootopt for those bits.
    //
    // ランタイムを指定しないソースに対して、すべての候補がこのランタイムとこれらの起動ビットを
    // 書く時に真。その時ソースには #runtime の行もそのビットの #bootopt も要らない。
    internal bool WritesByDefault(string? runtime, uint bootOption) =>
        runtime != null
        && All(r =>
            r.DefaultRuntime == runtime && (bootOption & r.DefaultBootOption) == r.DefaultBootOption
        );

    // The boot bits every candidate sets by default.
    //
    // すべての候補が既定で立てる起動ビット。
    internal uint DefaultBootOption =>
        Candidates.Aggregate(uint.MaxValue, (bits, r) => bits & r.DefaultBootOption);

    // True when every candidate came with or after the given release, so it has what that
    // release added.
    //
    // すべての候補が指定したリリース以降のものであり、そのリリースが加えたものを持つ時に真。
    internal bool From(CompilerRelease release) => All(r => r.Order >= release.Order);

    // The candidates as a range, such as "3.1-3.32", for logs.
    //
    // ログ用に候補を "3.1-3.32" のような範囲で表す。
    public override string ToString() =>
        Candidates.Length == 1 ? Candidates[0].Name
        : Candidates.IsEmpty ? "?"
        : Candidates[0].Name + "-" + Candidates[^1].Name;

    // Narrows the candidates to the releases that could have written the evidence. Each rule
    // keeps the candidates it cannot rule out, and a rule that would leave none is skipped, so
    // an AX from a release missing in the table still gets the nearest answer.
    //
    // 証拠を書けたリリースに候補を絞る。各規則は否定できない候補を残し、候補を空にする規則は
    // 飛ばすため、表に無いリリースの AX でも最も近い答えを得る。
    internal static ReleaseProfile Infer(AxEvidence evidence)
    {
        // A word missing in the table, such as one from a later release, takes the rows of the
        // nearest word below it, which is what its compiler grew from.
        //
        // 表に無いワード(後のリリースのものなど)は、その下で最も近いワードの行を取る。その
        // コンパイラはそこから育ったものである。
        uint word = CompilerReleases
            .All.Select(r => r.FormatWord)
            .Where(w => w <= evidence.FormatWord)
            .DefaultIfEmpty(evidence.FormatWord)
            .Max();
        ImmutableArray<CompilerRelease> candidates = Keep(CompilerReleases.All, r =>
            r.FormatWord == word
        );
        if (evidence.NormalBuildMarks > 0)
        {
            candidates = Keep(candidates, r => r.Records != NormalBuildRecords.None);
        }

        if (evidence.SingleItemFlags)
        {
            candidates = Keep(candidates, r => r.Optprm == OptprmSupport.Known);
        }

        if (evidence.Frames is { } frames)
        {
            candidates = Keep(candidates, r => r.Frames == frames);
        }

        return new ReleaseProfile(candidates);
    }

    private static ImmutableArray<CompilerRelease> Keep(
        ImmutableArray<CompilerRelease> candidates,
        Func<CompilerRelease, bool> possible
    )
    {
        ImmutableArray<CompilerRelease> kept = [.. candidates.Where(possible)];
        return kept.IsEmpty ? candidates : kept;
    }

    private bool All(Func<CompilerRelease, bool> holds) => Candidates.All(holds);
}
