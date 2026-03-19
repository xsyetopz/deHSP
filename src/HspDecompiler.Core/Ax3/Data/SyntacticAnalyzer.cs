using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax3.Data.Analyzer;
using HspDecompiler.Core.Ax3.Data.Line;
using HspDecompiler.Core.Ax3.Data.PP;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax3.Data
{
    class SyntacticAnalyzer
    {
        int readingLine = 0;

        internal async Task<List<LogicalLine>> AnalyzeAsync(TokenCollection stream, AxData data, IDecompilerLogger logger, IProgressReporter progress, CancellationToken ct)
        {
            List<LogicalLine> ret = new List<LogicalLine>();
            SubAnalyzePreprocessor(ret, data);
            readingLine = ret.Count;
            while (!stream.NextIsEndOfStream)
            {
                await progress.YieldAsync(ct);
                readingLine++;
                LogicalLine? line = LogicalLineFactory.GetCodeToken(stream.GetLine());
                if (line != null)
                {
                    ret.Add(line);
                }
            }

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
            ret = ret.FindAll(IsVisible);
            for (int i = 0; i < ret.Count; i++)
            {
                if (!ret[i].CheckRpn())
                {
                    ret[i].AddError(Strings.RpnConversionFailed);
                }
            }

            // Fix #26: renamed subAnalyzeScoop → SubAnalyzeScope
            SubAnalyzeScope(ret);
            // Fix #27: renamed subAnalyzeLabel → SubAnalyzeLabel
            SubAnalyzeLabel(ret, data);

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
            ret[ret.Count - 1].Visible = false;
            ret = ret.FindAll(IsVisible);
            return ret;
        }

        // Fix #27: renamed subAnalyzePreprocessor → SubAnalyzePreprocessor
        private void SubAnalyzePreprocessor(List<LogicalLine> ret, AxData data)
        {
            if (data.Runtime != null)
            {
                ret.Add(new PreprocessorDeclaration(data.Runtime));
                ret.Add(new CommentLine());
            }
            if (data.Modules.Count != 0)
            {
                foreach (Function module in data.Modules)
                {
                    LogicalLine line = new PreprocessorDeclaration(module);
                    line.AddError(Strings.StructNotInHspSpec);
                    ret.Add(line);
                }
            }

            foreach (Usedll dll in data.Usedlls)
            {
                ret.Add(new PreprocessorDeclaration(dll));
                List<Function> funcs = dll.GetFunctions();
                if (funcs != null)
                {
                    foreach (Function func in funcs)
                    {
                        ret.Add(new PreprocessorDeclaration(func));
                    }
                }

                ret.Add(new CommentLine());
            }

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
        }

        // Fix #26+#27: renamed subAnalyzeScoop → SubAnalyzeScope, ScoopEnd → ScopeEnd
        private void SubAnalyzeScope(List<LogicalLine> ret)
        {
            for (int i = 0; i < ret.Count; i++)
            {
                IfStatement? scopeStart = ret[i] as IfStatement;
                if (scopeStart == null)
                {
                    continue;
                }

                if (scopeStart.JumpToOffset < 0)
                {
                    scopeStart.ScoopEndIsDefined = false;
                    scopeStart.AddError(Strings.AnalysisFailedEndNotStored);
                    continue;
                }
                int jumpToOffset = scopeStart.JumpToOffset;
                int jumpToLineNo = -1;
                for (int j = (i + 1); j < ret.Count; j++)
                {
                    if (ret[j].TokenOffset == jumpToOffset)
                    {
                        jumpToLineNo = j;
                        break;
                    }
                    if ((ret[j].TokenOffset != -1) && (ret[j].TokenOffset > jumpToOffset))
                    {
                        jumpToLineNo = -2;
                        break;
                    }
                }
                if (jumpToLineNo == -1)
                {
                    scopeStart.ScoopEndIsDefined = false;
                    scopeStart.AddError(string.Format(Strings.AnalysisFailedScopeEndExceedsCode, jumpToOffset.ToString("X08")));
                    continue;
                }
                if (jumpToLineNo == -2)
                {
                    scopeStart.ScoopEndIsDefined = false;
                    scopeStart.AddError(string.Format(Strings.AnalysisFailedScopeEndMidLine, jumpToOffset.ToString("X08")));
                    continue;
                }
                IfStatement? elseStatement = ret[jumpToLineNo - 1] as IfStatement;
                if (elseStatement != null)
                {
                    if ((scopeStart.isIfStatement) && (elseStatement.isElseStatement))
                    {
                        jumpToLineNo--;
                    }
                }
                // Fix #26: ScoopEnd → ScopeEnd
                ret.Insert(jumpToLineNo, new ScopeEnd());
                scopeStart.ScoopEndIsDefined = true;
            }
        }

        // Fix #27: renamed subAnalyzeLabel → SubAnalyzeLabel
        // Fix #25: calls renamed DeleteInvisibleLabels
        private void SubAnalyzeLabel(List<LogicalLine> ret, AxData data)
        {
            foreach (LogicalLine line in ret)
            {
                line.CheckLabel();
            }

            data.DeleteInvisibleLabels();
            data.RenameLables();
            int i = 0;
            foreach (Label label in data.Labels)
            {
                if (label.TokenOffset == -1)
                {
                    continue;
                }

                while ((i < ret.Count) && ((ret[i].TokenOffset == -1) || (label.TokenOffset > ret[i].TokenOffset)))
                {
                    i++;
                }

                if ((i > 0) && (ret[i] is IfStatement))
                {
                    IfStatement? ifStatement = ret[i] as IfStatement;
                    // Fix #26: ScoopEnd → ScopeEnd
                    if ((ret[i - 1] is ScopeEnd) && (ifStatement?.isElseStatement == true))
                    {
                        i--;
                    }
                }
                ret.Insert(i, new PreprocessorDeclaration(label));
                continue;
            }
        }

        private bool IsVisible(LogicalLine line)
        {
            return line.Visible;
        }
    }
}
