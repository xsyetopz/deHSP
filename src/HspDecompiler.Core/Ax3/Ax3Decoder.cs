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
using HspDecompiler.Core.Ax3.Data.PP;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Pipeline;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax3;

internal class Ax3Decoder : IAxDecoder, IIncludeSource
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

    public IReadOnlyList<IncludeFile> Includes { get; private set; } = [];

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
    )
    {
        var data = new AxData { Style = Style };
        var stringLines = new List<string>();
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
            List<string> directives = CompileDirectives(data);
            if (names != null)
            {
                stringLines.AddRange(Layout(lines, directives, data, names, restored));
            }
            else
            {
                stringLines.AddRange(directives);
                stringLines.AddRange(Indented(lines, data));
            }
        }
        catch (SystemException e)
        {
            throw new HspDecoderException("AxData", Strings.UnexpectedError, e);
        }

        return stringLines;
    }

    private static IEnumerable<string> Indented(List<LogicalLine> lines, AxData data) =>
        lines
            .Where(line => line.Visible)
            .Select(line => new string('\t', line.TabCount) + data.Scope.Render(line));

    private IncludeNames IncludeNamesOf(AxData data) =>
        new([.. data.DebugInfo.FileNames.Select(data.DecodeName)], MainFileName);

    private List<string> Layout(
        List<LogicalLine> lines,
        List<string> directives,
        AxData data,
        IncludeNames names,
        HashSet<int> restored
    )
    {
        SourceLayoutResult result = SourceLayout.Build(lines, directives, data, names, restored);
        Includes = result.Includes;
        return result.Main;
    }

    private static List<string> CompileDirectives(AxData data)
    {
        List<string> directives = [];

        // hspcmp's default optcode drops each #func and #cfunc the source never names (except
        // onexit ones), each #module nothing refers to, and stores each repeated string once
        // (codegen.cpp CMPMODE_OPTCODE). An uncalled import, an unused module or a repeated
        // string in the AX means optcode was off, so the source turns it off too; otherwise the
        // default reproduces the pooled data segment. Checked against each release: hspcmp 3.0a
        // has no optcode and ignores the directive, 3.1 to 3.51 drop imports and modules but
        // store every string, and pooling starts with 3.6. A repeated string therefore says
        // nothing about an AX older than 0x360, and nothing at all about a 0x300 one, where the
        // directive would only take a source line from a debug build.
        //
        // hspcmp の既定の optcode は、ソースが一度も名前を出さない #func・#cfunc(onexit 以外)
        // と、何からも参照されない #module を削り、同じ文字列を一度だけ格納する(codegen.cpp の
        // CMPMODE_OPTCODE)。呼ばれない import・使われないモジュール・重複した文字列が AX に
        // あれば optcode は切られていたため、ソースでも切る。それ以外は既定のままにすると共有
        // されたデータセグメントを再現できる。各リリースで確かめた結果、hspcmp 3.0a には
        // optcode が無くこの指定を無視し、3.1〜3.51 は import とモジュールを削るが文字列は
        // すべて格納し、共有は 3.6 からである。そのため重複した文字列は 0x360 より古い AX
        // では何も示さず、0x300 の AX では何も示さない。そこでは指定はデバッグビルドの
        // ソース行を 1 行使うだけである。
        uint version = data.Header!.Version;
        bool pooled = version >= AxHeader.Version36 && data.HasRepeatedStrings;
        bool dropped =
            version >= AxHeader.Version31
            && (HasUncalledImport(data) || data.Scope.HasUnusedModule);
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
        // without it, such as the HSP 3.0 build behind Elona. The option arrived in 3.3: 3.0a
        // ignores the directive, and 3.1 to 3.21 reject it as an illegal option name. 3.1 to
        // 3.4 share format 0x301, and a 3.3 or 3.4 build with optprm 0 is the same AX as a
        // 3.1 to 3.21 build, so the directive is written only from 0x350, where every hspcmp
        // knows it.
        //
        // hspcmp は既定で 1 つのリテラルだけの引数に印を付ける（codegen.cpp の
        // CMPMODE_OPTPRM、EXFLG_0）。印の付いたリテラルが無い AX は、このオプションを
        // 切ったか、Elona を作った HSP 3.0 のようにこの機能の無いコンパイラで作られている。
        // このオプションは 3.3 で加わった。3.0a は指定を無視し、3.1〜3.21 は不正なオプション
        // 名として拒否する。3.1〜3.4 は形式 0x301 を共有し、optprm 0 の 3.3・3.4 のビルドは
        // 3.1〜3.21 のビルドと同じ AX になるため、すべての hspcmp が知っている 0x350 から
        // だけ指定を書く。
        if (!data.HasSingleItemFlags && version >= AxHeader.Version35)
        {
            directives.Add(PlainParametersDirective);
        }

        directives.AddRange(
            BootOptionDirectives.Build(
                data.Header!.BootOption,
                data.ScriptStrings.IsUtf8,
                data.UsesAwait
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
