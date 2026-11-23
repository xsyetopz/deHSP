using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax3.Data.Analyzer;
using HspDecompiler.Core.Ax3.Data.Line;
using HspDecompiler.Core.Ax3.Data.PP;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax3.Data;

internal class SyntacticAnalyzer
{
    private static readonly CompositeFormat s_scopeEndExceedsCodeFormat = CompositeFormat.Parse(
        Strings.AnalysisFailedScopeEndExceedsCode
    );
    private static readonly CompositeFormat s_scopeEndMidLineFormat = CompositeFormat.Parse(
        Strings.AnalysisFailedScopeEndMidLine
    );

    internal async Task<List<LogicalLine>> AnalyzeAsync(
        TokenCollection stream,
        AxData data,
        IDecompilerLogger logger,
        IProgressReporter progress,
        CancellationToken ct
    )
    {
        var ret = new List<LogicalLine>();
        int importAnchor = SubAnalyzePreprocessor(ret, data);
        while (!stream.NextIsEndOfStream)
        {
            await progress.YieldAsync(ct).ConfigureAwait(false);
            LogicalLine? line = LogicalLineFactory.GetCodeToken(stream.GetLine());
            if (line != null)
            {
                ret.Add(line);
            }
        }

        HideGhostLines(ret);
        ret = ret.FindAll(IsVisible);
        for (int i = 0; i < ret.Count; i++)
        {
            if (!ret[i].CheckRpn())
            {
                ret[i].AddError(Strings.RpnConversionFailed);
            }
        }

        SubAnalyzeScope(ret);
        SubAnalyzeLabel(ret, data);
        ImportPlacement.Insert(ret, data, importAnchor);
        ModuleBlocks.Rebuild(ret, data);

        SetTabCounts(ret);

        ReportErrors(ret, logger);
        ret[^1].Visible = false;
        ret = ret.FindAll(IsVisible);
        return ret;
    }

    private static void HideGhostLines(List<LogicalLine> ret)
    {
        for (int i = 0; i < ret.Count; i++)
        {
            if (ret[i].HasFlagIsGhost)
            {
                ret[i].Visible = false;
            }

            if ((ret[i].HasFlagGhostGoto) && (i != (ret.Count - 1)))
            {
                ret[i + 1].Visible = false;
            }
        }
    }

    private static void SetTabCounts(List<LogicalLine> ret)
    {
        int tabCount = 1;
        for (int i = 0; i < ret.Count; i++)
        {
            if (ret[i].TabDecrement)
            {
                tabCount--;
            }

            ret[i].TabCount = tabCount;
            if (ret[i].TabIncrement)
            {
                tabCount++;
            }
        }
    }

    private static void ReportErrors(List<LogicalLine> ret, IDecompilerLogger logger)
    {
        for (int i = 0; i < ret.Count; i++)
        {
            if (ret[i].GetErrorMes().Count != 0)
            {
                foreach (string errMes in ret[i].GetErrorMes())
                {
                    logger.Warning(errMes, i + 1);
                }
            }
        }
    }

    private static int SubAnalyzePreprocessor(List<LogicalLine> ret, AxData data)
    {
        if (data.Runtime != null && !DirectivePlanner.RuntimeIsDefault(data))
        {
            ret.Add(new PreprocessorDeclaration(data.Runtime));
            ret.Add(new CommentLine());
        }
        if (data.Modules.Count != 0)
        {
            foreach (Function module in data.Modules)
            {
                ret.Add(new PreprocessorDeclaration(module));
            }
        }

        int importAnchor = ret.Count;
        foreach (PlugIn plugin in data.PlugIns)
        {
            ret.Add(new PreprocessorDeclaration(plugin));
            Dictionary<int, Cmd> cmds = plugin.GetCmds();
            foreach (Cmd cmd in cmds.Values)
            {
                ret.Add(new PreprocessorDeclaration(cmd));
            }
            ret.Add(new CommentLine());
        }
        return importAnchor;
    }

    private static void SubAnalyzeScope(List<LogicalLine> ret)
    {
        for (int i = 0; i < ret.Count; i++)
        {
            var scopeStart = ret[i] as IfStatement;
            if (scopeStart == null)
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
            int jumpToLineNo = FindScopeEnd(ret, i + 1, jumpToOffset);
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
            if (EndsAtElse(scopeStart, ret[jumpToLineNo - 1] as IfStatement, jumpToOffset))
            {
                jumpToLineNo--;
            }
            ret.Insert(jumpToLineNo, new ScopeEnd());
            scopeStart.ScopeEndIsDefined = true;
        }
    }

    // Returns -1 when the jump target lies past the code and -2 when it falls inside a line.
    //
    // 飛び先がコードの外なら -1、行の途中なら -2 を返す。
    private static int FindScopeEnd(List<LogicalLine> ret, int from, int jumpToOffset)
    {
        for (int j = from; j < ret.Count; j++)
        {
            if (ret[j].TokenOffset == jumpToOffset)
            {
                return j;
            }

            if ((ret[j].TokenOffset != -1) && (ret[j].TokenOffset > jumpToOffset))
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

    private static void SubAnalyzeLabel(List<LogicalLine> ret, AxData data)
    {
        foreach (LogicalLine line in ret)
        {
            line.CheckLabel();
        }

        data.DeleteInvisibleLabels();
        data.RenameLabels();
        int i = 0;
        foreach (Label label in data.Labels)
        {
            if (label.TokenOffset == -1)
            {
                continue;
            }

            while (
                (i < ret.Count)
                && ((ret[i].TokenOffset == -1) || (label.TokenOffset > ret[i].TokenOffset))
            )
            {
                i++;
            }

            if ((i > 0) && (i < ret.Count) && (ret[i] is IfStatement))
            {
                var ifStatement = ret[i] as IfStatement;
                if ((ret[i - 1] is ScopeEnd) && (ifStatement?.IsElseStatement == true))
                {
                    i--;
                }
            }
            ret.Insert(i, new PreprocessorDeclaration(label));
        }
    }

    private bool IsVisible(LogicalLine line) => line.Visible;
}
