using System.Collections.Generic;
using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Ax3.Data.Line;
using HspDecompiler.Core.Ax3.Data.PP;
using HspDecompiler.Core.Ax3.Naming;

namespace HspDecompiler.Core.Ax3.Analysis;

// Keeps the labels a written line needs, names them, and puts each in front of the line at its
// code offset. A label at an else that follows a closed block goes in front of that block's end
// instead.
//
// 書き出す行が要るラベルを残して名前を付け、それぞれをコード位置の行の前に置く。閉じた
// ブロックに続く else にあるラベルは、代わりにそのブロックの終わりの前に置く。
internal sealed class LabelPass : IAnalysisPass
{
    public void Run(AnalysisState state)
    {
        List<LogicalLine> lines = state.Lines;
        foreach (LogicalLine line in lines)
        {
            line.CheckLabel();
        }

        state.Data.DeleteInvisibleLabels();
        NameAssigner.NameLabels(state.Data);
        int i = 0;
        foreach (Label label in state.Data.Labels)
        {
            if (label.TokenOffset == -1)
            {
                continue;
            }

            while (
                (i < lines.Count)
                && ((lines[i].TokenOffset == -1) || (label.TokenOffset > lines[i].TokenOffset))
            )
            {
                i++;
            }

            if (
                (i > 0)
                && (i < lines.Count)
                && lines[i] is IfStatement { IsElseStatement: true }
                && (lines[i - 1] is ScopeEnd)
            )
            {
                i--;
            }

            lines.Insert(i, new PreprocessorDeclaration(label));
        }
    }
}
