using System.Collections.Generic;
using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax3.Analysis;

// Drops the lines hspcmp generated: a ghost line itself, and the line after a ghost goto.
//
// hspcmp が生成した行を外す。ゴースト行そのものと、ゴーストの goto の次の行。
internal sealed class GhostLinePass : IAnalysisPass
{
    public void Run(AnalysisState state)
    {
        List<LogicalLine> lines = state.Lines;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].HasFlagIsGhost)
            {
                lines[i].Visible = false;
            }

            if ((lines[i].HasFlagGhostGoto) && (i != (lines.Count - 1)))
            {
                lines[i + 1].Visible = false;
            }
        }

        state.Lines = lines.FindAll(line => line.Visible);
    }
}

// Marks each line whose expressions do not convert from reverse Polish notation.
//
// 式が逆ポーランド記法から戻せない行に印を付ける。
internal sealed class RpnPass : IAnalysisPass
{
    public void Run(AnalysisState state)
    {
        foreach (LogicalLine line in state.Lines)
        {
            if (!line.CheckRpn())
            {
                line.AddError(Strings.RpnConversionFailed);
            }
        }
    }
}

// Places the import declarations the code calls (ImportPlacement).
//
// コードが呼ぶインポート宣言を置く(ImportPlacement)。
internal sealed class ImportPass : IAnalysisPass
{
    public void Run(AnalysisState state) =>
        ImportPlacement.Insert(state.Lines, state.Data, state.ImportAnchor);
}

// Rebuilds the #module blocks the compiler flattened (ModuleBlocks).
//
// コンパイラが平らにした #module ブロックを組み直す(ModuleBlocks)。
internal sealed class ModulePass : IAnalysisPass
{
    public void Run(AnalysisState state) => ModuleBlocks.Rebuild(state.Lines, state.Data);
}

// Gives each line its indent from the blocks around it.
//
// 周りのブロックから各行の字下げを決める。
internal sealed class IndentPass : IAnalysisPass
{
    public void Run(AnalysisState state)
    {
        int tabCount = 1;
        foreach (LogicalLine line in state.Lines)
        {
            if (line.TabDecrement)
            {
                tabCount--;
            }

            line.TabCount = tabCount;
            if (line.TabIncrement)
            {
                tabCount++;
            }
        }
    }
}

// Reports every error a line collected, then drops the last line: hspcmp appends an end
// command to every script, so it is not part of the source.
//
// 各行が集めたエラーを報告し、最後の行を外す。hspcmp はどのスクリプトの末尾にも終了の命令を
// 足すため、その行はソースの一部ではない。
internal sealed class ReportPass : IAnalysisPass
{
    public void Run(AnalysisState state)
    {
        List<LogicalLine> lines = state.Lines;
        for (int i = 0; i < lines.Count; i++)
        {
            foreach (string error in lines[i].GetErrorMes())
            {
                state.Logger.Warning(error, i + 1);
            }
        }

        lines[^1].Visible = false;
        state.Lines = lines.FindAll(line => line.Visible);
    }
}
