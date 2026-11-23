using System.Collections.Generic;
using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Ax3.Data.PP;
using HspDecompiler.Core.Releases;

namespace HspDecompiler.Core.Ax3;

// The facts the compile directives depend on. Each one is read from the AX except
// UnusedModule, which the layout finds while it rebuilds the #module blocks.
//
// コンパイル指示文が依存する事実。UnusedModule 以外は AX から読む。UnusedModule はレイアウトが
// #module ブロックを組み直す時に見つける。
internal sealed record DirectiveFacts(
    ReleaseProfile Release,
    bool RepeatedStrings,
    bool UncalledImport,
    bool UnusedModule,
    bool VariableNames,
    bool DebugLines,
    bool SingleItemFlags,
    uint BootOption,
    bool Utf8Strings,
    bool UsesAwait,
    uint DefaultBootBits
);

// Decides which #cmpopt and #bootopt lines a source needs so that hspcmp writes the same AX
// again. The decision is a function of DirectiveFacts alone, so a new compiler quirk is one
// more fact and one more rule here.
//
// hspcmp が同じ AX をもう一度書くためにソースが要る #cmpopt と #bootopt の行を決める。決定は
// DirectiveFacts だけの関数であり、新しいコンパイラの癖は事実と規則を 1 つずつここに足す。
internal static class DirectivePlanner
{
    internal const string KeepUnusedImportsDirective = "#cmpopt optcode 0";
    internal const string KeepVariableNamesDirective = "#cmpopt varname 1";
    internal const string PlainParametersDirective = "#cmpopt optprm 0";

    internal static DirectiveFacts FactsOf(AxData data, bool unusedModule) =>
        new(
            data.Release,
            data.HasRepeatedStrings,
            HasUncalledImport(data),
            unusedModule,
            data.HasVariableNames,
            data.DebugInfo.HasLines,
            data.HasSingleItemFlags,
            data.Header!.BootOption,
            data.ScriptStrings.IsUtf8,
            data.UsesAwait,
            DefaultBootBitsOf(data)
        );

    // True when every candidate compiler writes the AX's runtime for a source that names none,
    // so the #runtime line can go.
    //
    // すべての候補のコンパイラが、ランタイムを指定しないソースに AX のランタイムを書く時に真。
    // その時 #runtime の行は要らない。
    internal static bool RuntimeIsDefault(AxData data) =>
        data.Release.WritesByDefault(data.Runtime?.Name, data.Header!.BootOption);

    private static uint DefaultBootBitsOf(AxData data) =>
        RuntimeIsDefault(data) ? data.Release.DefaultBootOption : 0;

    internal static List<string> Plan(DirectiveFacts facts)
    {
        List<string> directives = [];

        // hspcmp's default optcode drops each #func and #cfunc the source never names (except
        // onexit ones), each #module nothing refers to, and, from 3.6, stores each repeated string
        // once (codegen.cpp CMPMODE_OPTCODE; CompilerRelease.Optcode per release). An uncalled
        // import, an unused module or a repeated string in the AX means optcode was off, so the
        // source turns it off too, but only where every candidate release would have dropped or
        // pooled them: a repeated string from 3.5 or an import kept by 3.0a says nothing.
        //
        // hspcmp の既定の optcode は、ソースが一度も名前を出さない #func・#cfunc(onexit 以外)
        // と、何からも参照されない #module を削り、3.6 からは同じ文字列を一度だけ格納する
        // (codegen.cpp の CMPMODE_OPTCODE、リリースごとには CompilerRelease.Optcode)。呼ばれない
        // import・使われないモジュール・重複した文字列が AX にあれば optcode は切られていたため
        // ソースでも切る。ただしすべての候補リリースがそれを削るか共有する時だけで、3.5 の重複
        // した文字列や 3.0a が残した import は何も示さない。
        ReleaseProfile release = facts.Release;
        bool pooled = release.PoolsStrings && facts.RepeatedStrings;
        bool dropped = release.DropsUnused && (facts.UncalledImport || facts.UnusedModule);
        if (pooled || dropped)
        {
            directives.Add(KeepUnusedImportsDirective);
        }

        // hspcmp writes variable names into the debug segment only in debug mode or under
        // varname (codegen.cpp PutDIVars), so names in the AX ask for the same on recompile. Line
        // records come only from debug mode, which writes the names already, so such an AX
        // needs no directive, and the source keeps its first line free for its own code.
        //
        // hspcmp が変数名をデバッグ情報に書くのはデバッグモードか varname 指定時だけ
        // （codegen.cpp の PutDIVars）。AX に変数名があれば再コンパイルでも同じ指定をする。行の
        // 記録はデバッグモードでしか作られず、そのモードは変数名も書くため、その AX には指定が
        // 要らず、ソースの先頭行はソース自身のコードに空けておける。
        if (facts.VariableNames && !facts.DebugLines)
        {
            directives.Add(KeepVariableNamesDirective);
        }

        // hspcmp flags a parameter made of one literal by default (codegen.cpp CMPMODE_OPTPRM,
        // EXFLG_0). An AX with no flagged literal had the option off or came from a compiler
        // without it, such as the HSP 3.0 build behind Elona. 3.0a ignores the directive and 3.1
        // to 3.21 reject it (CompilerRelease.Optprm), so it is written only when every candidate
        // release knows it; a 3.3 build with optprm 0 is the same AX as a 3.21 build.
        //
        // hspcmp は既定で 1 つのリテラルだけの引数に印を付ける(codegen.cpp の
        // CMPMODE_OPTPRM、EXFLG_0)。印の付いたリテラルが無い AX は、このオプションを
        // 切ったか、Elona を作った HSP 3.0 のようにこの機能の無いコンパイラで作られている。
        // 3.0a は指定を無視し、3.1〜3.21 は拒否する(CompilerRelease.Optprm)ため、すべての
        // 候補リリースが知っている時だけ書く。optprm 0 の 3.3 のビルドは 3.21 のビルドと同じ AX
        // になる。
        if (!facts.SingleItemFlags && release.KnowsOptprm)
        {
            directives.Add(PlainParametersDirective);
        }

        directives.AddRange(
            BootOptionDirectives.Build(
                facts.BootOption,
                facts.Utf8Strings,
                facts.UsesAwait,
                facts.DefaultBootBits
            )
        );
        return directives;
    }

    internal static bool HasUncalledImport(AxData data) =>
        data.Functions.Exists(func =>
            func.IsDllFunction
            && func.FirstUseOffset < 0
            && (func.Flags & FunctionFlags.OnExit) == 0
        );
}
