using System;
using System.Collections.Immutable;
using System.Linq;

namespace HspDecompiler.Core.Releases;

// What one AX shows about the compiler that wrote it and the options it ran with, gathered once
// after the code is read (Ax3.EvidenceReader). The release inference reads FormatWord,
// NormalBuildMarks, SingleItemFlags, Frames, Keywords, RedefinedBuiltin, Runtime and
// CodeOnEveryLine; the directive planner reads the rest as well.
// FormatWord is the header version. NormalBuildMarks counts the line records of a build without
// -d, SingleItemFlags says some one-literal parameter carries hspcmp's optprm flag, and Frames is
// the parameter layout a user function shows, if any does. RepeatedStrings, UncalledImport,
// VariableNames, Utf8Strings and UsesAwait record what optcode, varname, UTF-8 output and await
// left in the AX; DebugLines says the AX is a -d build.
//
// 1 つの AX が書いたコンパイラと、その時のオプションについて示すこと。コードを読んだ後に一度
// だけ集める(Ax3.EvidenceReader)。リリースの推定は FormatWord・NormalBuildMarks・
// SingleItemFlags・Frames・Keywords・RedefinedBuiltin・Runtime・CodeOnEveryLine を読み、
// 指示文の計画はそれ以外も読む。FormatWord はヘッダーのバージョン、NormalBuildMarks は -d 無しのビルドの行の記録の数、SingleItemFlags は 1 リテラルの
// 引数のどれかに hspcmp の optprm の印があること、Frames はユーザー関数が示す引数の並べ方
// (示すものがあれば)。RepeatedStrings・UncalledImport・VariableNames・Utf8Strings・UsesAwait は
// optcode・varname・UTF-8 出力・await が AX に残したもの、DebugLines は AX が -d のビルドである
// こと。
internal sealed record AxEvidence(
    uint FormatWord,
    int NormalBuildMarks,
    bool SingleItemFlags,
    FrameLayout? Frames = null
)
{
    internal bool RepeatedStrings { get; init; }

    internal bool UncalledImport { get; init; }

    internal bool VariableNames { get; init; }

    internal bool DebugLines { get; init; }

    internal uint BootOption { get; init; }

    internal bool Utf8Strings { get; init; }

    internal bool UsesAwait { get; init; }

    // A #deffunc takes a built-in's name, which only a release that accepts the replacement
    // compiles.
    //
    // #deffunc が組み込み命令の名前を取る。置き換えを受け付けるリリースだけがこれをコンパイルする。
    internal bool RedefinedBuiltin { get; init; }

    // The runtime the AX names, and whether a debug build shows a source with code on every
    // line. Such a source had no line for a #runtime, so the compiler wrote the runtime by
    // itself.
    //
    // AX が示すランタイムと、デバッグビルドがすべての行にコードのあるソースを示すこと。その
    // ソースには #runtime を書く行が無いため、ランタイムはコンパイラ自身が書いている。
    internal string? Runtime { get; init; }

    internal bool CodeOnEveryLine { get; init; }

    // The releases that have every keyword the code uses; null when nothing was read.
    //
    // コードが使うすべてのキーワードを持つリリース。何も読んでいなければ null。
    internal ReleaseSpan? Keywords { get; init; }

    // The index an HSP 1 AX gives its first user variable; null when the AX does not show it.
    //
    // HSP 1 の AX が最初のユーザー変数に与える番号。AX がそれを示さない時は null。
    internal int? FirstVariable { get; init; }
}

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

    internal bool RedefinesBuiltins => All(r => r.RedefinesBuiltins);

    internal bool IncludesHspdef => All(r => r.IncludesHspdef);

    internal bool LabelsRepeat => All(r => r.LabelsRepeat);

    // A release without brace blocks reads "{" as a token, so a block goes on its if line.
    //
    // ブロックの括弧の無いリリースは "{" をトークンとして読むため、ブロックは if の行に置く。
    internal bool BraceBlocks => All(r => r.BraceBlocks);

    // The longest string, in escaped source bytes, that every candidate keeps whole on one line;
    // 0 when none cuts one.
    //
    // すべての候補が 1 行で切らずに読む文字列の最長(エスケープしたソースのバイト数)。どの候補も
    // 切らない時は 0。
    internal int InlineStringLimit =>
        Candidates
            .Where(r => r.InlineStringLimit > 0)
            .Select(r => r.InlineStringLimit)
            .DefaultIfEmpty(0)
            .Min();

    // The format word alone sets the token packing, so the candidates never disagree on it.
    //
    // トークンの詰め方は形式のワードだけで決まるため、候補の間で食い違うことは無い。
    internal Ax2TokenLayout TokenLayout =>
        Candidates.IsEmpty ? Ax2TokenLayout.NineBitValue : Candidates[0].TokenLayout;

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

        if (evidence.Keywords is { } keywords)
        {
            candidates = Keep(candidates, keywords.Contains);
        }

        if (evidence.FirstVariable is { } first)
        {
            candidates = Keep(candidates, r => r.FirstVariable == first);
        }

        if (evidence.RedefinedBuiltin)
        {
            candidates = Keep(candidates, r => r.RedefinesBuiltins);
        }

        if (evidence.CodeOnEveryLine && (evidence.Runtime != null))
        {
            candidates = Keep(candidates, r =>
                r.DefaultRuntime == evidence.Runtime
                && (evidence.BootOption & r.DefaultBootOption) == r.DefaultBootOption
            );
        }

        return new ReleaseProfile(candidates);
    }

    // The releases of one format word, which is all the header tells before the code is read.
    //
    // 1 つの形式のワードのリリース。コードを読む前にヘッダーが示すのはこれだけである。
    internal static ReleaseProfile ForFormat(uint word) => Infer(new AxEvidence(word, 0, false));

    // The release the caller names, when the evidence left it a candidate. A hint is advisory:
    // it settles what the AX cannot show, such as 3.2 against 3.32, and never overrides what the
    // AX does show, so a wrong hint costs nothing but the warning its null return leads to.
    //
    // 呼び出し側が指定したリリース。証拠がそれを候補に残した時だけ使う。指定は助言であり、
    // 3.2 と 3.32 のように AX が示せないことを決め、AX が示すことは覆さない。そのため誤った
    // 指定の損は、null を返した結果の警告だけである。
    internal ReleaseProfile? Pin(string hint)
    {
        ImmutableArray<CompilerRelease> pinned = [.. Candidates.Where(r => r.Names(hint))];
        return pinned.IsEmpty ? null : new ReleaseProfile(pinned);
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
