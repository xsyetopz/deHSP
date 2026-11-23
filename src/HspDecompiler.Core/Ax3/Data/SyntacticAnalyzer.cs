using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax3.Analysis;
using HspDecompiler.Core.Ax3.Data.Analyzer;
using HspDecompiler.Core.Ax3.Data.Line;
using HspDecompiler.Core.Ax3.Data.PP;

namespace HspDecompiler.Core.Ax3.Data;

// Builds the logical lines from the token stream, then runs the analysis passes over them in
// order. Each pass reads what the earlier ones left: LabelPass looks for the scope ends
// ScopePass inserts, and IndentPass runs after every pass that inserts lines.
//
// トークン列から論理行を組み、解析の段を順に実行する。各段は前の段が残したものを読む。
// LabelPass は ScopePass が挿入したスコープの終わりを探し、IndentPass は行を挿入する
// すべての段の後に実行する。
internal sealed class SyntacticAnalyzer
{
    private static readonly IAnalysisPass[] s_passes =
    [
        new GhostLinePass(),
        new RpnPass(),
        new ScopePass(),
        new LabelPass(),
        new ImportPass(),
        new ModulePass(),
        new IndentPass(),
        new ReportPass(),
    ];

    internal static async Task<List<LogicalLine>> AnalyzeAsync(
        TokenCollection stream,
        AxData data,
        IDecompilerLogger logger,
        IProgressReporter progress,
        CancellationToken ct
    )
    {
        var lines = new List<LogicalLine>();
        int importAnchor = AddDeclarations(lines, data);
        while (!stream.NextIsEndOfStream)
        {
            await progress.YieldAsync(ct).ConfigureAwait(false);
            LogicalLine? line = LogicalLineFactory.GetCodeToken(stream.GetLine());
            if (line != null)
            {
                lines.Add(line);
            }
        }

        var state = new AnalysisState(lines, data, logger, importAnchor);
        foreach (IAnalysisPass pass in s_passes)
        {
            pass.Run(state);
        }

        return state.Lines;
    }

    // The #runtime, #module and plug-in declarations the tables hold, ahead of the code. Returns
    // where the imports begin.
    //
    // テーブルが持つ #runtime・#module・プラグインの宣言をコードの前に置き、インポートが
    // 始まる位置を返す。
    private static int AddDeclarations(List<LogicalLine> lines, AxData data)
    {
        if (data.Runtime != null && !DirectivePlanner.RuntimeIsDefault(data))
        {
            lines.Add(new PreprocessorDeclaration(data.Runtime));
            lines.Add(new CommentLine());
        }

        foreach (Function module in data.Modules)
        {
            lines.Add(new PreprocessorDeclaration(module));
        }

        int importAnchor = lines.Count;
        foreach (PlugIn plugin in data.PlugIns)
        {
            lines.Add(new PreprocessorDeclaration(plugin));
            foreach (Cmd cmd in plugin.GetCmds().Values)
            {
                lines.Add(new PreprocessorDeclaration(cmd));
            }

            lines.Add(new CommentLine());
        }

        return importAnchor;
    }
}
