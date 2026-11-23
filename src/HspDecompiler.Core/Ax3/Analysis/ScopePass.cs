using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Ax3.Data.Line;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax3.Analysis;

// Closes each if and else block at the line its jump lands on.
//
// 各 if・else ブロックを、そのジャンプが着く行で閉じる。
internal sealed class ScopePass : IAnalysisPass
{
    private static readonly CompositeFormat s_scopeEndExceedsCodeFormat = CompositeFormat.Parse(
        Strings.AnalysisFailedScopeEndExceedsCode
    );

    private static readonly CompositeFormat s_scopeEndMidLineFormat = CompositeFormat.Parse(
        Strings.AnalysisFailedScopeEndMidLine
    );

    public void Run(AnalysisState state)
    {
        List<LogicalLine> lines = state.Lines;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i] is not IfStatement scopeStart)
            {
                continue;
            }

            if (scopeStart.JumpToOffset < 0)
            {
                scopeStart.ScopeEndIsDefined = false;
                scopeStart.AddError(Strings.AnalysisFailedEndNotStored);
                continue;
            }

            int jumpToOffset = scopeStart.JumpToOffset;
            int jumpToLineNo = FindScopeEnd(lines, i + 1, jumpToOffset);
            if (jumpToLineNo < 0)
            {
                scopeStart.ScopeEndIsDefined = false;
                scopeStart.AddError(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        jumpToLineNo == -1 ? s_scopeEndExceedsCodeFormat : s_scopeEndMidLineFormat,
                        jumpToOffset.ToString("X08", CultureInfo.InvariantCulture)
                    )
                );
                continue;
            }

            if (EndsAtElse(scopeStart, lines[jumpToLineNo - 1] as IfStatement, jumpToOffset))
            {
                jumpToLineNo--;
            }

            lines.Insert(jumpToLineNo, new ScopeEnd());
            scopeStart.ScopeEndIsDefined = true;
        }
    }

    // Returns -1 when the jump target lies past the code and -2 when it falls inside a line.
    //
    // 飛び先がコードの外なら -1、行の途中なら -2 を返す。
    private static int FindScopeEnd(List<LogicalLine> lines, int from, int jumpToOffset)
    {
        for (int j = from; j < lines.Count; j++)
        {
            if (lines[j].TokenOffset == jumpToOffset)
            {
                return j;
            }

            if ((lines[j].TokenOffset != -1) && (lines[j].TokenOffset > jumpToOffset))
            {
                return -2;
            }
        }

        return -1;
    }

    // A block that ends where an else jump ends closes in front of that else. hspcmp also
    // accepts a second else after an else block ("} else { a } else { b }"), which gives the
    // same layout with an else opening the scope; Elona's start.hsp has one. An else that jumps
    // to the same target is an empty "else {}" nested at the end of the block, not a sibling.
    //
    // ブロックの終端が else ジャンプの直後なら、そのブロックは else の手前で閉じる。hspcmp は
    // else ブロックの後ろにもう 1 つ else を置く形（"} else { a } else { b }"）も受け付け、
    // else が開いたスコープで同じ配置になる。Elona の start.hsp に実例がある。同じ飛び先へ
    // 飛ぶ else はブロック末尾に入れ子になった空の "else {}" であり、兄弟ではない。
    private static bool EndsAtElse(IfStatement scopeStart, IfStatement? before, int jumpToOffset) =>
        before is { IsElseStatement: true }
        && (
            scopeStart.IsIfStatement
            || (scopeStart.IsElseStatement && (before.JumpToOffset != jumpToOffset))
        );
}
