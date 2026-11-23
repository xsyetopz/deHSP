using System.Collections.Generic;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax3.Data;

namespace HspDecompiler.Core.Ax3.Analysis;

// One step of the syntactic analysis over the logical lines. Each pass reads and rewrites the
// shared state, and SyntacticAnalyzer runs them in a fixed order, so a release quirk that needs
// its own step becomes one more pass in the list rather than a branch inside another.
//
// 論理行に対する構文解析の 1 段。各段は共有の状態を読み書きし、SyntacticAnalyzer が決まった
// 順に実行する。そのため独自の段が要るリリースの癖は、別の段の中の分岐ではなく一覧の 1 段になる。
internal interface IAnalysisPass
{
    void Run(AnalysisState state);
}

// The lines under analysis and what the passes share. A pass that filters lines replaces the
// list.
//
// 解析中の行と各段が共有するもの。行を絞る段は一覧を置き換える。
internal sealed class AnalysisState(
    List<LogicalLine> lines,
    AxData data,
    IDecompilerLogger logger,
    int importAnchor
)
{
    internal List<LogicalLine> Lines { get; set; } = lines;

    internal AxData Data { get; } = data;

    internal IDecompilerLogger Logger { get; } = logger;

    // The line index where the import declarations begin, before any pass inserts lines.
    //
    // どの段も行を挿入する前の、インポート宣言が始まる行の位置。
    internal int ImportAnchor { get; } = importAnchor;
}
