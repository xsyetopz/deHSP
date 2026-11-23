using System;
using System.Collections.Generic;
using System.Linq;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax2.Data;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Releases;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax2.Writing;

// Turns the token words of one AxData into source lines: marks the labels a line needs, writes
// each statement with its indent and closing braces, then places the declarations and splits the
// lines into the files the text segment names (IncludeLayout).
//
// 1 つの AxData のトークンのワードをソースの行にする。行が要るラベルに印を付け、各文を字下げと
// 閉じ括弧付きで書き、宣言を置いて、テキストのセグメントが名指すファイルに行を分ける
// (IncludeLayout)。
internal sealed class Ax2Emitter
{
    private readonly AxData _data;
    private readonly ReleaseProfile _release;
    private readonly List<string> _undefs = [];
    private readonly List<SourceLine> _lines = [];
    private readonly HashSet<int> _referencedText = [];
    private readonly List<int> _ifEnd = [];
    private int _tabNo = 1;
    private int _lineAnchor = -1;

    private Ax2Emitter(AxData data, ReleaseProfile release)
    {
        _data = data;
        _release = release;
    }

    internal static DecodedSource Emit(AxData data, ReleaseProfile release, string? mainFileName)
    {
        var emitter = new Ax2Emitter(data, release);
        emitter.EmitLines();
        return emitter.BuildSource(mainFileName);
    }

    private void EmitLines()
    {
        var cursor = new TokenCursor(_data);
        MarkShadowedBuiltins();
        ScanTokens(cursor);
        EnableLabels();

        SourceLine? line;
        cursor.SetZero();
        while ((line = GetLine(cursor)) != null)
        {
            _lines.Add(line.Value);
        }
    }

    // HSP 2 lets a #deffunc replace a built-in after #undef; the original stays callable as
    // name@hsp, so both the #undef and the @hsp suffix are restored when every candidate release
    // accepts the replacement.
    //
    // HSP 2 では #undef の後に #deffunc で組み込み命令を置き換えられる。元の命令は name@hsp で
    // 呼べるため、すべての候補のリリースが置き換えを受け付ける時は #undef と @hsp の両方を
    // 復元する。
    private void MarkShadowedBuiltins()
    {
        foreach (Deffunc deffunc in _data.Deffuncs)
        {
            if (
                _release.RedefinesBuiltins
                && Token.IsBuiltinName(deffunc.Name)
                && _data.ShadowedBuiltins.Add(deffunc.Name)
            )
            {
                _undefs.Add("#undef " + deffunc.Name);
            }

            _referencedText.Add(deffunc.NameOffset);
        }
    }

    private void ScanTokens(TokenCursor cursor)
    {
        cursor.SetZero();
        Token? token;
        try
        {
            while ((token = cursor.GetNext()) != null)
            {
                if (token.LabelIndex != -1)
                {
                    _data.Labels[token.LabelIndex].LoadCount += 1;
                }

                if (token._hiddenLabelIndex != -1)
                {
                    _data.Labels[token._hiddenLabelIndex].IsCompilerGenerated = true;
                }

                if (token.StringOffset >= 0)
                {
                    _referencedText.Add(token.StringOffset);
                }
            }
        }
        catch (Exception e)
        {
            throw new HspDecoderException("AxHeader", Strings.UnrecoverableErrorDuringLabelRead, e);
        }
    }

    // hspcmp records every source label in the object table, referenced or not, so an
    // unreferenced one is still restored. Entry 0 is a reserved slot hspcmp leaves
    // uninitialized, and the labels after repeat, break and continue are compiler generated.
    //
    // hspcmp は参照の有無に関わらずソース上のラベルをすべてオブジェクトテーブルに記録する
    // ため、未参照のラベルも復元する。0 番は hspcmp が初期化しない予約枠で、repeat・break・
    // continue の後ろのラベルはコンパイラが生成したものである。
    private void EnableLabels()
    {
        int tokenCount = _data.TokenData.Length / 2;
        Label[] labels = _data.Labels;
        for (int i = 0; i < labels.Length; i++)
        {
            Label label = labels[i];
            label.Enabled =
                (label.LoadCount > 0)
                || (
                    (i != 0)
                    && !label.IsCompilerGenerated
                    && (label.TokenIndex >= 0)
                    && (label.TokenIndex <= tokenCount)
                );
        }
    }

    // A #uselib line opens each run of #func lines from one DLL, since that is what selects the
    // DLL the following #func lines bind to. A DLL without functions keeps its line at the top.
    //
    // #uselib は後続の #func が結び付く DLL を選ぶため、同じ DLL の #func の連なりごとに先頭に
    // 置く。関数の無い DLL は先頭に残す。
    private List<SourceLine> GetDeclarations()
    {
        var declarations = new List<SourceLine>();
        Func[] funcs = _data.Funcs;
        Dll[] dlls = _data.Dlls;
        for (int i = 0; i < dlls.Length; i++)
        {
            if (!Array.Exists(funcs, f => f.DllIndex == i))
            {
                declarations.Add(new SourceLine(dlls[i].ToString(), -1));
            }
        }

        int currentDll = -1;
        foreach (Func func in funcs.OrderBy(f => f.NameOffset))
        {
            _referencedText.Add(func.NameOffset);
            if (
                (func.DllIndex != currentDll)
                && (func.DllIndex >= 0)
                && (func.DllIndex < dlls.Length)
            )
            {
                currentDll = func.DllIndex;
                declarations.Add(new SourceLine(dlls[currentDll].ToString(), func.NameOffset));
            }

            declarations.Add(new SourceLine(func.ToString(), func.NameOffset));
        }

        return declarations;
    }

    private List<FileSwitch> GetFileSwitches()
    {
        byte[] stringData = _data.StringData;
        var switches = new List<FileSwitch>();
        int offset = 0;
        while (offset < stringData.Length)
        {
            int end = Array.IndexOf(stringData, (byte)0, offset);
            if (end < 0)
            {
                end = stringData.Length;
            }

            if (!_referencedText.Contains(offset))
            {
                string text = _data.GetString(offset);
                if (IncludeLayout.IsSourceFileName(text))
                {
                    switches.Add(new FileSwitch(offset, text));
                }
            }

            offset = end + 1;
        }

        return switches;
    }

    private DecodedSource BuildSource(string? mainFileName)
    {
        List<SourceLine> declarations = GetDeclarations();
        List<SourceLine> merged = IncludeLayout.Merge(_lines, declarations);
        DecodedSource source = IncludeLayout.Split(merged, GetFileSwitches(), mainFileName);
        source.Main.InsertRange(0, _undefs);
        return source;
    }

    private static string GetTab(int tab) => new('\t', tab);

    private void EmitLabel(Token token)
    {
        foreach (Label label in _data.Labels)
        {
            if (!label.Enabled || (token.Id != label.TokenIndex))
            {
                continue;
            }

            int deffunc = label.Deffunc;
            int anchor = deffunc >= 0 ? _data.Deffuncs[deffunc].NameOffset : -1;
            _lines.Add(new SourceLine(label.Name, anchor));
            label.Enabled = false;
        }
    }

    private string FormatTokenOutput(Token first, TokenCursor cursor)
    {
        string line = GetTab(_tabNo) + first.GetString();
        NoteAnchor(first);

        if (first._isLineend)
        {
            return line;
        }

        Token? token;
        while ((token = cursor.GetNext()) != null)
        {
            string add = token.GetString();
            NoteAnchor(token);
            line += token.IsArg ? ", " : " ";
            line += add;
            if (token._isLineend)
            {
                break;
            }
        }

        return line;
    }

    private void NoteAnchor(Token token)
    {
        if ((_lineAnchor < 0) && (token.StringOffset >= 0))
        {
            _lineAnchor = token.StringOffset;
        }
    }

    private SourceLine? GetLine(TokenCursor cursor)
    {
        Token? token = cursor.GetNext();
        if (token == null)
        {
            return null;
        }

        for (int i = 0; i < _ifEnd.Count; i++)
        {
            if ((token.Id == _ifEnd[i]) || (token.IfJumpId == _ifEnd[i]))
            {
                _tabNo--;
                _lines.Add(new SourceLine(GetTab(_tabNo) + "}", -1));
                _ifEnd.RemoveAt(i);
                i--;
            }
        }

        EmitLabel(token);

        // hspcmp appends an end token to every script, so the last one is not part of the source.
        //
        // hspcmp はすべてのスクリプトの末尾に end を追加するため、最後の end はソースに含まれない。
        if (token.IsEnd && cursor.AtEnd)
        {
            return null;
        }

        bool tabPlus = token.TabPlus;
        int ifJumpTo = token._ifJumpTo;
        if (token.TabMinus)
        {
            _tabNo--;
        }

        _lineAnchor = -1;
        string line = FormatTokenOutput(token, cursor);

        if (tabPlus)
        {
            _tabNo++;
        }

        if (ifJumpTo >= 0)
        {
            line += " {";
            _ifEnd.Add(ifJumpTo);
        }

        return new SourceLine(line, _lineAnchor);
    }
}
