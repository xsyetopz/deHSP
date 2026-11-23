using System.Collections.Immutable;

namespace HspDecompiler.Core.Releases;

// What hspcmp's default optcode does (codegen.cpp CMPMODE_OPTCODE): nothing in 3.0a, drop each
// unnamed #func, #cfunc and unused #module from 3.1, and store each repeated string once from 3.6.
//
// hspcmp の既定の optcode の働き(codegen.cpp の CMPMODE_OPTCODE)。3.0a では何もせず、3.1 から
// 名前の出ない #func・#cfunc と使われない #module を削り、3.6 から同じ文字列を一度だけ格納する。
internal enum OptcodeMode
{
    None,
    DropsUnused,
    DropsUnusedAndPoolsStrings,
}

// How hspcmp treats "#cmpopt optprm": 3.0a ignores it, 3.1 to 3.21 reject the option name, and
// from 3.3 it turns off the single-literal parameter flag (codegen.cpp CMPMODE_OPTPRM, EXFLG_0),
// which is on by default.
//
// hspcmp の "#cmpopt optprm" の扱い。3.0a は無視し、3.1〜3.21 はオプション名を拒否し、3.3 からは
// 既定で有効な 1 リテラル引数の印(codegen.cpp の CMPMODE_OPTPRM、EXFLG_0)を切る。
internal enum OptprmSupport
{
    Ignored,
    Rejected,
    Known,
}

// The line records hspcmp writes into a build without -d. 3.1 to 3.32 store each "##N" the
// preprocessor emits (token.cpp ExpandLine): one for each #module and #global, one after a macro
// expansion that broke into more lines, and from 3.3 one where each {"..."} string ends. 3.4 and
// later write records only in debug mode.
//
// -d 無しのビルドに hspcmp が書く行の記録。3.1〜3.32 はプリプロセッサが出す各 "##N" を記録する
// (token.cpp の ExpandLine)。各 #module と #global に 1 つ、より多くの行に分かれたマクロ展開の
// 後に 1 つ、3.3 からは各 {"..."} 文字列の終わりに 1 つ。3.4 以降はデバッグモードでだけ記録する。
internal enum NormalBuildRecords
{
    None,
    Modules,
    ModulesAndStringEnds,
}

// One hspcmp release and what it does that shows in, or decides, the AX it writes. Order is the
// release's position among all of them, so "from 3.2" compares orders. FormatWord is the header
// version hspcmp writes, which several releases share.
//
// 1 つの hspcmp リリースと、それが書く AX に現れる、または AX を決める振る舞い。Order は
// 全リリース中の位置で、"3.2 から" は Order を比べる。FormatWord は hspcmp が書くヘッダーの
// バージョンで、複数のリリースが共有する。
internal sealed record CompilerRelease(
    string Name,
    int Order,
    uint FormatWord,
    OptcodeMode Optcode,
    OptprmSupport Optprm,
    NormalBuildRecords Records
);

// Every hspcmp release deHSP knows, oldest first. A difference between compilers is a column here
// and a question on ReleaseProfile; the stage that depends on it asks the question. The values
// come from the OpenHSP sources and from compiling probe programs with each release.
//
// deHSP が知る hspcmp の全リリース(古い順)。コンパイラ間の違いはここの列と ReleaseProfile の
// 問いになり、それに依存する段がその問いを尋ねる。値は OpenHSP のソースと、各リリースで試験
// プログラムをコンパイルした結果による。
internal static class CompilerReleases
{
    // The header every hspcmp, 2.x and 3.x alike, includes before the main file.
    //
    // 2.x・3.x のどの hspcmp もメインファイルの前に取り込むヘッダー。
    internal const string ImplicitHeader = "hspdef.as";

    internal const uint Word30 = 0x300;
    internal const uint Word31 = 0x301;
    internal const uint Word35 = 0x350;
    internal const uint Word36 = 0x360;

    internal static CompilerRelease Hsp30 { get; } =
        new("3.0", 0, Word30, OptcodeMode.None, OptprmSupport.Ignored, NormalBuildRecords.None);

    internal static CompilerRelease Hsp30A { get; } =
        new("3.0a", 1, Word30, OptcodeMode.None, OptprmSupport.Ignored, NormalBuildRecords.None);

    internal static CompilerRelease Hsp31 { get; } =
        new(
            "3.1",
            2,
            Word31,
            OptcodeMode.DropsUnused,
            OptprmSupport.Rejected,
            NormalBuildRecords.Modules
        );

    internal static CompilerRelease Hsp32 { get; } =
        new(
            "3.2",
            3,
            Word31,
            OptcodeMode.DropsUnused,
            OptprmSupport.Rejected,
            NormalBuildRecords.Modules
        );

    internal static CompilerRelease Hsp321 { get; } =
        new(
            "3.21",
            4,
            Word31,
            OptcodeMode.DropsUnused,
            OptprmSupport.Rejected,
            NormalBuildRecords.Modules
        );

    internal static CompilerRelease Hsp33 { get; } =
        new(
            "3.3",
            5,
            Word31,
            OptcodeMode.DropsUnused,
            OptprmSupport.Known,
            NormalBuildRecords.ModulesAndStringEnds
        );

    internal static CompilerRelease Hsp331 { get; } =
        new(
            "3.31",
            6,
            Word31,
            OptcodeMode.DropsUnused,
            OptprmSupport.Known,
            NormalBuildRecords.ModulesAndStringEnds
        );

    internal static CompilerRelease Hsp332 { get; } =
        new(
            "3.32",
            7,
            Word31,
            OptcodeMode.DropsUnused,
            OptprmSupport.Known,
            NormalBuildRecords.ModulesAndStringEnds
        );

    internal static CompilerRelease Hsp34 { get; } =
        new(
            "3.4",
            8,
            Word31,
            OptcodeMode.DropsUnused,
            OptprmSupport.Known,
            NormalBuildRecords.None
        );

    internal static CompilerRelease Hsp35 { get; } =
        new(
            "3.5",
            9,
            Word35,
            OptcodeMode.DropsUnused,
            OptprmSupport.Known,
            NormalBuildRecords.None
        );

    internal static CompilerRelease Hsp351 { get; } =
        new(
            "3.51",
            10,
            Word35,
            OptcodeMode.DropsUnused,
            OptprmSupport.Known,
            NormalBuildRecords.None
        );

    internal static CompilerRelease Hsp36 { get; } =
        new(
            "3.6",
            11,
            Word36,
            OptcodeMode.DropsUnusedAndPoolsStrings,
            OptprmSupport.Known,
            NormalBuildRecords.None
        );

    internal static CompilerRelease Hsp37 { get; } =
        new(
            "3.7",
            12,
            Word36,
            OptcodeMode.DropsUnusedAndPoolsStrings,
            OptprmSupport.Known,
            NormalBuildRecords.None
        );

    internal static CompilerRelease Hsp38B2 { get; } =
        new(
            "3.8β2",
            13,
            Word36,
            OptcodeMode.DropsUnusedAndPoolsStrings,
            OptprmSupport.Known,
            NormalBuildRecords.None
        );

    internal static ImmutableArray<CompilerRelease> All { get; } =
    [
        Hsp30,
        Hsp30A,
        Hsp31,
        Hsp32,
        Hsp321,
        Hsp33,
        Hsp331,
        Hsp332,
        Hsp34,
        Hsp35,
        Hsp351,
        Hsp36,
        Hsp37,
        Hsp38B2,
    ];
}
