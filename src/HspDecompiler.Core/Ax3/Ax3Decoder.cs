using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Ax3.Data.Analyzer;
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
            data.LoadEnd();
            logger.Write(Strings.SyntacticAnalysis);
            var synt = new SyntacticAnalyzer();
            List<LogicalLine> lines = await synt.AnalyzeAsync(stream, data, logger, progress, ct)
                .ConfigureAwait(false);
            logger.Write(Strings.CreatingOutputFile);
            // The AX keeps every import the original source declared, so recompiling must not
            // prune unreferenced #func entries (hspcmp codegen.cpp CMPMODE_OPTCODE).
            //
            // AX には元のソースが宣言した import がすべて残るため、再コンパイル時に未参照の
            // #func を削ってはならない（hspcmp codegen.cpp の CMPMODE_OPTCODE）。
            stringLines.Add(KeepUnusedImportsDirective);
            // hspcmp writes variable names into the debug segment only in debug mode or under
            // varname (codegen.cpp PutDIVars), so names in the AX ask for the same on recompile.
            //
            // hspcmp が変数名をデバッグ情報に書くのはデバッグモードか varname 指定時だけ
            // （codegen.cpp の PutDIVars）。AX に変数名があれば再コンパイルでも同じ指定をする。
            if (data.HasVariableNames)
            {
                stringLines.Add(KeepVariableNamesDirective);
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
                stringLines.Add(PlainParametersDirective);
            }
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
}
