using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Ax3.Data.Analyzer;
using HspDecompiler.Core.Ax3.Data.Layout;
using HspDecompiler.Core.Ax3.Data.Line;
using HspDecompiler.Core.Ax3.Data.PP;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Pipeline;
using HspDecompiler.Core.Releases;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax3;

internal class Ax3Decoder : ISourceDecoder
{
    internal const string KeepUnusedImportsDirective = "#cmpopt optcode 0";
    internal const string KeepVariableNamesDirective = "#cmpopt varname 1";
    internal const string PlainParametersDirective = "#cmpopt optprm 0";

    private Hsp3Dictionary? _dictionary;

    // The name the main source is written under; a rebuilt include must not take it.
    //
    // メインのソースを書き出す名前。復元した取り込みファイルはこの名前を使えない。
    internal string MainFileName { get; init; } = "start.hsp";

    // The directory of the caller's common files (hspcmp --compath), used to restore the
    // #include of a common header that left no code.
    //
    // 呼び出し側の共通ファイルのディレクトリ(hspcmp の --compath)。コードを残さなかった
    // 共通ヘッダーの #include を復元するのに使う。
    internal string? CommonDirectory { get; init; }

    internal SourceStyle Style { get; init; } = SourceStyle.Default;

    internal Hsp3Dictionary? Dictionary
    {
        get => _dictionary;
        set => _dictionary = value;
    }

    public async Task<List<string>> DecodeAsync(
        BinaryReader reader,
        IDecompilerLogger logger,
        IProgressReporter progress,
        CancellationToken ct = default
    ) => (await DecodeSourceAsync(reader, logger, progress, ct).ConfigureAwait(false)).Main;

    public async Task<DecodedSource> DecodeSourceAsync(
        BinaryReader reader,
        IDecompilerLogger logger,
        IProgressReporter progress,
        CancellationToken ct
    )
    {
        var data = new AxData { Style = Style };
        try
        {
            logger.Write(Strings.AnalyzingHeader);
            data.LoadStart(reader, _dictionary);
            data.ReadHeader();
            logger.Write(Strings.AnalyzingPreprocessor);
            data.ReadPreprocessor(_dictionary);
            logger.Write(Strings.LexicalAnalysis);
            var lex = new LexicalAnalyzer(_dictionary!);
            TokenCollection stream = lex.Analyze(data);
            data.Release = ReleaseProfile.Infer(
                new AxEvidence(
                    data.Header!.Version,
                    data.DebugInfo.HasLines ? 0 : data.DebugInfo.Marks.Count,
                    data.HasSingleItemFlags
                )
            );
            data.ScriptStrings.Resolve(
                data.Runtime != null,
                BootOptionDirectives.IsUtf8Runtime(data.Header!.BootOption)
            );
            IncludeNames? names = data.DebugInfo.HasLines ? IncludeNamesOf(data) : null;
            HashSet<int> restored =
                names == null ? [] : CommonIncludes.Attach(data, names, CommonDirectory);
            DebugNames.ApplyParameterNames(data);
            data.LoadEnd();
            logger.Write(Strings.SyntacticAnalysis);
            var synt = new SyntacticAnalyzer();
            List<LogicalLine> lines = await synt.AnalyzeAsync(stream, data, logger, progress, ct)
                .ConfigureAwait(false);
            logger.Write(Strings.CreatingOutputFile);
            DecodedSource source = Write(lines, data, names, restored);
            WarnStructs(source, logger);
            return source;
        }
        catch (SystemException e)
        {
            throw new HspDecoderException("AxData", Strings.UnexpectedError, e);
        }
    }

    // A debug build places each line by its record. A normal build from hspcmp 3.1 to 3.32
    // records only its #module, #global and {"..."} lines, which MarkedLayout rebuilds when they
    // fit; other builds write the lines in order.
    //
    // デバッグビルドは各行を記録に従って置く。hspcmp 3.1〜3.32 の通常ビルドは #module・
    // #global・{"..."} の行だけを記録し、収まれば MarkedLayout がそれを組み直す。それ以外の
    // ビルドは行を順に書く。
    private DecodedSource Write(
        List<LogicalLine> lines,
        AxData data,
        IncludeNames? names,
        HashSet<int> restored
    )
    {
        if (names != null)
        {
            bool unused = lines.Exists(line => line is ModuleStart { Used: false });
            return SourceLayout.Build(
                lines,
                CompileDirectives(data, unused),
                data,
                names,
                restored
            );
        }

        return Marked(lines, data)
            ?? DecodedSource.Single([.. CompileDirectives(data, false), .. Indented(lines, data)]);
    }

    private DecodedSource? Marked(List<LogicalLine> lines, AxData data) =>
        data.DebugInfo.Marks.Count > 0
        && data.Release.MayWriteNormalBuildRecords
        && MarkedLayout.Match(lines, data, HasUncalledImport(data)) is { } marked
            ? marked.Write(CompileDirectives(data, marked.HasUnusedModule), MainFileName)
            : null;

    // A #struct line is deHSP's own spelling of a module's members; HSP documents no such
    // directive. A rebuilt #module writes the members itself and leaves none behind.
    //
    // #struct 行はモジュールのメンバーを表す deHSP 独自の書き方で、HSP はこの指示文を文書化
    // していない。組み直した #module はメンバーを自分で書き、#struct 行を残さない。
    private static void WarnStructs(DecodedSource source, IDecompilerLogger logger)
    {
        foreach (
            List<string> file in source
                .Includes.Select(include => include.Lines)
                .Prepend(source.Main)
        )
        {
            for (int i = 0; i < file.Count; i++)
            {
                if (file[i].StartsWith("#struct ", StringComparison.Ordinal))
                {
                    logger.Warning(Strings.StructNotInHspSpec, i + 1);
                }
            }
        }
    }

    private static IEnumerable<string> Indented(List<LogicalLine> lines, AxData data) =>
        lines
            .Where(line => line.Visible)
            .Select(line => new string('\t', line.TabCount) + data.Scope.Render(line));

    private IncludeNames IncludeNamesOf(AxData data) =>
        new([.. data.DebugInfo.FileNames.Select(data.DecodeName)], MainFileName);

    private static List<string> CompileDirectives(AxData data, bool unusedModule)
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
        ReleaseProfile release = data.Release;
        bool pooled = release.PoolsStrings && data.HasRepeatedStrings;
        bool dropped = release.DropsUnused && (HasUncalledImport(data) || unusedModule);
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
        if (data.HasVariableNames && !data.DebugInfo.HasLines)
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
        if (!data.HasSingleItemFlags && release.KnowsOptprm)
        {
            directives.Add(PlainParametersDirective);
        }

        directives.AddRange(
            BootOptionDirectives.Build(
                data.Header!.BootOption,
                data.ScriptStrings.IsUtf8,
                data.UsesAwait,
                data.Runtime?.Name
            )
        );
        return directives;
    }

    private static bool HasUncalledImport(AxData data) =>
        data.Functions.Exists(func =>
            func.IsDllFunction
            && func.FirstUseOffset < 0
            && (func.Flags & FunctionFlags.OnExit) == 0
        );
}
