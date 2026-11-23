using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Ax3.Data.Analyzer;
using HspDecompiler.Core.Ax3.Data.PP;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax3;

internal class Ax3Decoder : IAxDecoder
{
    internal const string KeepUnusedImportsDirective = "#cmpopt optcode 0";
    internal const string KeepVariableNamesDirective = "#cmpopt varname 1";
    internal const string PlainParametersDirective = "#cmpopt optprm 0";

    private Hsp3Dictionary? _dictionary;

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
        var data = new AxData();
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
            data.LoadEnd();
            logger.Write(Strings.SyntacticAnalysis);
            var synt = new SyntacticAnalyzer();
            List<LogicalLine> lines = await synt.AnalyzeAsync(stream, data, logger, progress, ct)
                .ConfigureAwait(false);
            logger.Write(Strings.CreatingOutputFile);
            stringLines.AddRange(CompileDirectives(data));
            foreach (LogicalLine line in lines)
            {
                if (line.Visible)
                {
                    string str = new('\t', line.TabCount);
                    stringLines.Add(str + line);
                }
            }
        }
        catch (SystemException e)
        {
            throw new HspDecoderException("AxData", Strings.UnexpectedError, e);
        }

        return stringLines;
    }

    private static List<string> CompileDirectives(AxData data)
    {
        List<string> directives = [];

        // hspcmp's default optcode drops each #func and #cfunc the source never names (except
        // onexit ones) and stores each repeated string once (codegen.cpp CMPMODE_OPTCODE). An
        // uncalled import or a repeated string in the AX means optcode was off, so the source
        // turns it off too; otherwise the default reproduces the pooled data segment. hspcmp 3.5
        // never pools, so its repeated strings ask for optcode 0, which it compiles to the same
        // bytes. The output never holds #module blocks, so optcode's module pruning cannot apply.
        //
        // hspcmp の既定の optcode は、ソースが一度も名前を出さない #func・#cfunc(onexit 以外)
        // を削り、同じ文字列を一度だけ格納する(codegen.cpp の CMPMODE_OPTCODE)。呼ばれない
        // import か重複した文字列が AX にあれば optcode は切られていたため、ソースでも切る。
        // それ以外は既定のままにすると共有されたデータセグメントを再現できる。hspcmp 3.5 は
        // 文字列を共有しないため重複から optcode 0 を書くが、3.5 はそれでも同じバイト列を
        // 出力する。出力に #module
        // ブロックは無いため、optcode によるモジュールの削除は起きない。
        if (data.HasRepeatedStrings || HasUncalledImport(data))
        {
            directives.Add(KeepUnusedImportsDirective);
        }

        // hspcmp writes variable names into the debug segment only in debug mode or under
        // varname (codegen.cpp PutDIVars), so names in the AX ask for the same on recompile.
        //
        // hspcmp が変数名をデバッグ情報に書くのはデバッグモードか varname 指定時だけ
        // （codegen.cpp の PutDIVars）。AX に変数名があれば再コンパイルでも同じ指定をする。
        if (data.HasVariableNames)
        {
            directives.Add(KeepVariableNamesDirective);
        }

        // hspcmp flags a parameter made of one literal by default (codegen.cpp CMPMODE_OPTPRM,
        // EXFLG_0). An AX with no flagged literal had the option off or came from a compiler
        // without it, such as the HSP 3.0 build behind Elona.
        //
        // hspcmp は既定で 1 つのリテラルだけの引数に印を付ける（codegen.cpp の
        // CMPMODE_OPTPRM、EXFLG_0）。印の付いたリテラルが無い AX は、このオプションを
        // 切ったか、Elona を作った HSP 3.0 のようにこの機能の無いコンパイラで作られている。
        if (!data.HasSingleItemFlags)
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
