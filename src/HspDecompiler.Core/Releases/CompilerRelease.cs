using System;
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

// How hspcmp 2.x packs a token into its 16-bit word (Ghidra: hspcmp.dll's token writer). 2.5 puts
// an 8-bit value in the low byte and the line-head, argument and type bits above it, and follows a
// larger value with 16 bits (high byte 0x80) or 32 bits (0xC0). 2.55 shifts the type and flag bits
// up one, so the value takes nine bits, and follows a larger one with 32 bits (high bit set).
//
// hspcmp 2.x がトークンを 16 ビットのワードに詰める方法(Ghidra で見た hspcmp.dll のトークンの
// 書き込み)。2.5 は 8 ビットの値を下位バイトに置き、その上に行頭・引数・型のビットを置き、より
// 大きな値には 16 ビット(上位バイト 0x80)か 32 ビット(0xC0)を続ける。2.55 は型と印のビットを
// 1 つ上へずらし、値は 9 ビットを取り、より大きな値には 32 ビット(最上位ビット)を続ける。
internal enum Ax2TokenLayout
{
    ByteValue,
    NineBitValue,
}

// One hspcmp build and what it does that shows in, or decides, the AX it writes. Order is the
// build's position among all of them, so "from 3.2" compares orders. FormatWord is the header
// version hspcmp writes, which several releases share; it also tells an HSP 2 row (0x206) from an
// HSP 3 row. Frames is how it lays out parameters. DefaultRuntime and DefaultBootOption are what
// it writes for a source that names no runtime: the 64-bit hspcmp.exe of 3.8β2 targets hsp3_64
// unless -x selects the 32-bit runtime. RedefinesBuiltins says a #deffunc may take a built-in's
// name after #undef, leaving the built-in callable as name@hsp; only the HSP 2 decoder asks it.
// TokenLayout is the HSP 2 token packing, and IncludesHspdef says hspcmp reads hspdef.as before
// the main file, which 2.6 began. LabelsRepeat says repeat carries the loop-end label that break
// and continue carry; 2.5 writes it only after those two.
//
// 1 つの hspcmp のビルドと、それが書く AX に現れる、または AX を決める振る舞い。Order は全ビルド
// 中の位置で、"3.2 から" は Order を比べる。FormatWord は hspcmp が書くヘッダーのバージョンで、
// 複数のリリースが共有し、HSP 2 の行(0x206)と HSP 3 の行も見分ける。Frames は引数の並べ方。
// DefaultRuntime と DefaultBootOption はランタイムを指定しないソースに書くもので、3.8β2 の
// 64 ビットの hspcmp.exe は -x で 32 ビットのランタイムを選ばない限り hsp3_64 を対象にする。
// RedefinesBuiltins は #undef の後に #deffunc が組み込み命令の名前を取れ、組み込み命令が
// name@hsp で呼べるままであること。これを尋ねるのは HSP 2 のデコーダーだけである。
// TokenLayout は HSP 2 のトークンの詰め方、IncludesHspdef は hspcmp がメインファイルの前に
// hspdef.as を読むことで、2.6 から始まった。LabelsRepeat は break と continue が持つループの
// 終わりのラベルを repeat も持つことで、2.5 はその 2 つの後にだけ書く。
internal sealed record CompilerRelease(
    string Name,
    int Order,
    uint FormatWord,
    OptcodeMode Optcode,
    OptprmSupport Optprm,
    NormalBuildRecords Records,
    FrameLayout Frames = FrameLayout.Narrow,
    string? DefaultRuntime = null,
    uint DefaultBootOption = 0,
    bool RedefinesBuiltins = false,
    Ax2TokenLayout TokenLayout = Ax2TokenLayout.NineBitValue,
    bool IncludesHspdef = true,
    bool LabelsRepeat = true
)
{
    // The major version, 2 or 3; releases of one major share a token encoding.
    //
    // メジャーバージョン(2 か 3)。同じメジャーのリリースはトークンの符号化を共有する。
    internal uint Major => FormatWord >> 8;

    // "3.8b2" names 3.8β2, since a command line may not carry the Greek letter.
    //
    // コマンドラインがギリシャ文字を通さないことがあるため、"3.8b2" は 3.8β2 を指す。
    internal bool Names(string name) =>
        Name.Equals(name, StringComparison.OrdinalIgnoreCase)
        || Name.Replace('β', 'b').Equals(name, StringComparison.OrdinalIgnoreCase);
}

// Every hspcmp release deHSP knows, oldest first. A difference between compilers is a column here
// and a question on ReleaseProfile; the stage that depends on it asks the question. The values
// come from the OpenHSP sources and from compiling probe programs with each release.
//
// deHSP が知る hspcmp の全リリース(古い順)。コンパイラ間の違いはここの列と ReleaseProfile の
// 問いになり、それに依存する段がその問いを尋ねる。値は OpenHSP のソースと、各リリースで試験
// プログラムをコンパイルした結果による。
internal static class CompilerReleases
{
    // The header every hspcmp from 2.6 includes before the main file.
    //
    // 2.6 以降のどの hspcmp もメインファイルの前に取り込むヘッダー。
    internal const string ImplicitHeader = "hspdef.as";

    internal const uint Word25 = 0x205;
    internal const uint Word255 = 0x255;
    internal const uint Word26 = 0x206;
    internal const uint Word30 = 0x300;
    internal const uint Word31 = 0x301;
    internal const uint Word35 = 0x350;
    internal const uint Word36 = 0x360;

    // The HSP 2 rows come from each release's hspcmp.dll: Ghidra for the format word and the
    // token packing, and probe builds under Wine for the rest. 2.55 writes 0x255, above the 0x206
    // that 2.6 and 2.61 share, so the words do not sort by release; the 2.6 distribution's
    // start.ax carries 0x255 because 2.55 built it. Only 2.61 compiles #undef followed by a
    // #deffunc of the built-in's name: 2.5 and 2.55 reject #undef, and 2.6 fails defining the
    // function. HSP 2 has none of the 3.x code-generation options.
    //
    // HSP 2 の行は各リリースの hspcmp.dll による。形式のワードとトークンの詰め方は Ghidra、残りは
    // Wine 上の試験ビルドで調べた。2.55 は 0x255 を書き、2.6 と 2.61 が共有する 0x206 より大きい
    // ため、ワードはリリース順に並ばない。2.6 の配布物の start.ax は 2.55 が作ったため 0x255 を
    // 持つ。#undef の後に組み込み命令の名前で #deffunc するソースをコンパイルするのは 2.61 だけで、
    // 2.5 と 2.55 は #undef を拒否し、2.6 は関数の定義で失敗する。HSP 2 には 3.x のコード生成の
    // オプションが無い。
    internal static CompilerRelease Hsp25 { get; } =
        new(
            "2.5",
            0,
            Word25,
            OptcodeMode.None,
            OptprmSupport.Ignored,
            NormalBuildRecords.None,
            TokenLayout: Ax2TokenLayout.ByteValue,
            IncludesHspdef: false,
            LabelsRepeat: false
        );

    internal static CompilerRelease Hsp255 { get; } =
        new(
            "2.55",
            1,
            Word255,
            OptcodeMode.None,
            OptprmSupport.Ignored,
            NormalBuildRecords.None,
            IncludesHspdef: false
        );

    internal static CompilerRelease Hsp26 { get; } =
        new("2.6", 2, Word26, OptcodeMode.None, OptprmSupport.Ignored, NormalBuildRecords.None);

    internal static CompilerRelease Hsp261 { get; } =
        new(
            "2.61",
            3,
            Word26,
            OptcodeMode.None,
            OptprmSupport.Ignored,
            NormalBuildRecords.None,
            RedefinesBuiltins: true
        );

    internal static CompilerRelease Hsp30 { get; } =
        new("3.0", 4, Word30, OptcodeMode.None, OptprmSupport.Ignored, NormalBuildRecords.None);

    internal static CompilerRelease Hsp30A { get; } =
        new("3.0a", 5, Word30, OptcodeMode.None, OptprmSupport.Ignored, NormalBuildRecords.None);

    internal static CompilerRelease Hsp31 { get; } =
        new(
            "3.1",
            6,
            Word31,
            OptcodeMode.DropsUnused,
            OptprmSupport.Rejected,
            NormalBuildRecords.Modules
        );

    internal static CompilerRelease Hsp32 { get; } =
        new(
            "3.2",
            7,
            Word31,
            OptcodeMode.DropsUnused,
            OptprmSupport.Rejected,
            NormalBuildRecords.Modules
        );

    internal static CompilerRelease Hsp321 { get; } =
        new(
            "3.21",
            8,
            Word31,
            OptcodeMode.DropsUnused,
            OptprmSupport.Rejected,
            NormalBuildRecords.Modules
        );

    internal static CompilerRelease Hsp33 { get; } =
        new(
            "3.3",
            9,
            Word31,
            OptcodeMode.DropsUnused,
            OptprmSupport.Known,
            NormalBuildRecords.ModulesAndStringEnds
        );

    internal static CompilerRelease Hsp331 { get; } =
        new(
            "3.31",
            10,
            Word31,
            OptcodeMode.DropsUnused,
            OptprmSupport.Known,
            NormalBuildRecords.ModulesAndStringEnds
        );

    internal static CompilerRelease Hsp332 { get; } =
        new(
            "3.32",
            11,
            Word31,
            OptcodeMode.DropsUnused,
            OptprmSupport.Known,
            NormalBuildRecords.ModulesAndStringEnds
        );

    internal static CompilerRelease Hsp34 { get; } =
        new(
            "3.4",
            12,
            Word31,
            OptcodeMode.DropsUnused,
            OptprmSupport.Known,
            NormalBuildRecords.None
        );

    internal static CompilerRelease Hsp35 { get; } =
        new(
            "3.5",
            13,
            Word35,
            OptcodeMode.DropsUnused,
            OptprmSupport.Known,
            NormalBuildRecords.None
        );

    internal static CompilerRelease Hsp351 { get; } =
        new(
            "3.51",
            14,
            Word35,
            OptcodeMode.DropsUnused,
            OptprmSupport.Known,
            NormalBuildRecords.None
        );

    internal static CompilerRelease Hsp36 { get; } =
        new(
            "3.6",
            15,
            Word36,
            OptcodeMode.DropsUnusedAndPoolsStrings,
            OptprmSupport.Known,
            NormalBuildRecords.None
        );

    internal static CompilerRelease Hsp37 { get; } =
        new(
            "3.7",
            16,
            Word36,
            OptcodeMode.DropsUnusedAndPoolsStrings,
            OptprmSupport.Known,
            NormalBuildRecords.None
        );

    // The HSP64 boot bit (hsp3struct.h HSPHED_BOOTOPT_HSP64) and the runtime name it pairs with.
    //
    // HSP64 の起動ビット(hsp3struct.h の HSPHED_BOOTOPT_HSP64)と、それと組になるランタイム名。
    internal const uint Hsp64BootOption = 0x40000;
    internal const string Hsp64Runtime = "hsp3_64";

    // The 3.8β2 hspcmp.dll, which the script editor loads; a 32-bit build.
    //
    // スクリプトエディタが読み込む 3.8β2 の hspcmp.dll。32 ビットのビルド。
    internal static CompilerRelease Hsp38B2 { get; } =
        new(
            "3.8β2",
            17,
            Word36,
            OptcodeMode.DropsUnusedAndPoolsStrings,
            OptprmSupport.Known,
            NormalBuildRecords.None
        );

    // The 3.8β2 command-line hspcmp.exe, a 64-bit build.
    //
    // 3.8β2 のコマンドラインの hspcmp.exe。64 ビットのビルド。
    internal static CompilerRelease Hsp38B2X64 { get; } =
        new(
            "3.8β2 x64",
            18,
            Word36,
            OptcodeMode.DropsUnusedAndPoolsStrings,
            OptprmSupport.Known,
            NormalBuildRecords.None,
            FrameLayout.Wide,
            Hsp64Runtime,
            Hsp64BootOption
        );

    internal static ImmutableArray<CompilerRelease> All { get; } =
    [
        Hsp25,
        Hsp255,
        Hsp26,
        Hsp261,
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
        Hsp38B2X64,
    ];
}
