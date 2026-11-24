using System;
using System.Collections.Generic;
using System.Globalization;
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
    private readonly Dictionary<int, SourceLine> _moduleOpens = [];
    private readonly List<(int Id, int Anchor)> _lineAnchors = [];
    private readonly ModuleScopes _scopes = new();
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

        if (!_release.BraceBlocks)
        {
            List<SourceLine> folded = LineIfs.Fold(_lines);
            _lines.Clear();
            _lines.AddRange(folded);
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
                && _data.IsBuiltinName(deffunc.Name)
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
        var moduleJumps = new List<(int Jump, int Label)>();
        var variableUses = new List<(int Id, int Variable)>();
        Token? previous = null;
        Token? token;
        try
        {
            while ((token = cursor.GetNext()) != null)
            {
                if (IsModuleJump(previous, token))
                {
                    moduleJumps.Add((previous!.Id, token.LabelIndex));
                }

                if (token.VariableIndex >= 0)
                {
                    variableUses.Add((token.Id, token.VariableIndex));
                }

                NoteAnchors(previous, token);
                previous = token;
                NoteReferences(token);
            }
        }
        catch (Exception e)
        {
            throw new HspDecoderException("AxHeader", Strings.UnrecoverableErrorDuringLabelRead, e);
        }

        RestoreModules(moduleJumps);
        _scopes.Own(variableUses);
        _scopes.OwnFuncs(_lineAnchors, _data.Funcs.Select(f => f.NameOffset));
    }

    // Records the anchor of each line the emitter will write, in order: a #deffunc label line
    // anchors at its name and a code line at its first string. Merge places the #func lines by
    // these anchors, which settles the module each #func line ends up in.
    //
    // エミッターが書く各行のアンカーを順に記録する。#deffunc のラベルの行はその名前に、コードの
    // 行は最初の文字列にアンカーを持つ。Merge はこのアンカーで #func の行を置くため、各 #func の
    // 行が入るモジュールがこれで決まる。
    private void NoteAnchors(Token? previous, Token token)
    {
        if ((previous == null) || token.IsLinehead)
        {
            foreach (Label label in _data.Labels.Where(l => l.Deffunc >= 0))
            {
                if (label.TokenIndex == token.Id)
                {
                    _lineAnchors.Add((token.Id, _data.Deffuncs[label.Deffunc].NameOffset));
                }
            }

            _lineAnchors.Add((token.Id, -1));
        }

        if ((token.StringOffset >= 0) && (_lineAnchors[^1].Anchor < 0))
        {
            _lineAnchors[^1] = (_lineAnchors[^1].Id, token.StringOffset);
        }
    }

    private void NoteReferences(Token token)
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

    // hspcmp 2.5 and 2.55 open a #module with a goto past the module's code, and #global places
    // that goto's label. That goto has no '*' operator before its label; a source goto may lack
    // it too, as in "goto x1", so this only finds the candidates.
    //
    // hspcmp 2.5 と 2.55 は #module でモジュールのコードを飛び越す goto を置き、#global がその
    // goto のラベルを置く。この goto のラベルの前には '*' 演算子が無い。"goto x1" のように
    // ソースの goto にも無いことがあるため、これは候補を見つけるだけである。
    private static bool IsModuleJump(Token? previous, Token token) =>
        (previous != null)
        && previous.IsGoto
        && previous.IsLinehead
        && (token.LabelIndex != -1)
        && token._isLineend;

    // Each module record pairs with one module jump in source order. The jump goes back to its
    // #module line, its label to #global, and each #deffunc drops the "@module" suffix hspcmp
    // appends to the names inside a module; calls keep it, which names the same function. A
    // #module without a name gets "m" and the count of such modules so far, so that name goes
    // back to a bare #module. When some record finds no jump, nothing is restored.
    //
    // 各モジュールの記録はソース順で 1 つのモジュールの goto と組になる。goto は #module の行に、
    // そのラベルは #global に戻し、各 #deffunc からは hspcmp がモジュール内の名前に付ける
    // "@モジュール" の接尾辞を外す。呼び出しは同じ関数を指すため接尾辞を残す。名前の無い
    // #module は "m" とそれまでの名前の無いモジュールの数を名前に得るため、その名前は名前の無い
    // #module に戻す。goto の見つからない記録があれば何も復元しない。
    private void RestoreModules(List<(int Jump, int Label)> candidates)
    {
        string[] modules = _data.Modules;
        List<(int Jump, int Label)>? jumps = MatchModuleJumps(candidates);
        if (jumps == null)
        {
            return;
        }

        int unnamed = 0;
        for (int i = 0; i < modules.Length; i++)
        {
            string name = modules[i];
            bool isAutoNamed = name == "m" + unnamed.ToString(CultureInfo.InvariantCulture);
            unnamed += isAutoNamed ? 1 : 0;
            string open = isAutoNamed ? "#module" : "#module \"" + name + "\"";
            _moduleOpens[jumps[i].Jump] = new SourceLine(open, NextAnchor(jumps[i].Jump));
            Label end = _data.Labels[jumps[i].Label];
            end.Name = "#global";
            _scopes.Add(jumps[i].Jump, end.TokenIndex, name);
        }

        foreach (Label label in _data.Labels.Where(l => l.Deffunc >= 0))
        {
            Deffunc deffunc = _data.Deffuncs[label.Deffunc];
            int at = deffunc.Name.LastIndexOf('@');
            if ((at > 0) && modules.Contains(deffunc.Name[(at + 1)..], StringComparer.Ordinal))
            {
                deffunc.Name = deffunc.Name[..at];
                label.Name = deffunc.ToString();
            }
        }
    }

    // The #module line takes the anchor of the next anchored line, so it stays in the file of the
    // module's first #deffunc rather than the parent's.
    //
    // #module の行は次のアンカー付きの行のアンカーを取る。そうすれば親のファイルではなく、
    // モジュールの最初の #deffunc のファイルに残る。
    private int NextAnchor(int jump) =>
        _lineAnchors
            .Where(l => (l.Id > jump) && (l.Anchor >= 0))
            .Select(l => l.Anchor)
            .FirstOrDefault(-1);

    // A module's jump is the last candidate before the module's first #deffunc whose label lies
    // past its last one; a source goto before #module that skips the module comes earlier. A
    // module without a #deffunc takes the next candidate.
    //
    // モジュールの goto は、そのモジュールの最初の #deffunc より前にあり、ラベルが最後の #deffunc
    // より後ろにある候補のうち最後のものである。#module の前でモジュールを飛び越すソースの goto は
    // それより前にある。#deffunc の無いモジュールは次の候補を取る。
    private List<(int Jump, int Label)>? MatchModuleJumps(List<(int Jump, int Label)> candidates)
    {
        var matched = new List<(int Jump, int Label)>();
        int next = 0;
        foreach (string module in _data.Modules)
        {
            int[] bodies = DeffuncPositions(module);
            int found = -1;
            for (int k = next; k < candidates.Count; k++)
            {
                (int jump, int label) = candidates[k];
                if ((bodies.Length == 0) || (jump > bodies[0]))
                {
                    found = bodies.Length == 0 ? k : found;
                    break;
                }

                found = _data.Labels[label].TokenIndex > bodies[^1] ? k : found;
            }

            if (found < 0)
            {
                return null;
            }

            matched.Add(candidates[found]);
            next = found + 1;
        }

        return matched;
    }

    private int[] DeffuncPositions(string module) =>
        [
            .. _data
                .Labels.Where(l =>
                    (l.Deffunc >= 0)
                    && _data
                        .Deffuncs[l.Deffunc]
                        .Name.EndsWith("@" + module, StringComparison.Ordinal)
                )
                .Select(l => l.TokenIndex)
                .Order(),
        ];

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
        DecodedSource source = IncludeLayout.Split(
            merged,
            GetFileSwitches(),
            mainFileName,
            _release.IncludesHspdef
        );
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
        string? module = _scopes.At(first.Id);
        string line = GetTab(_tabNo) + Text(first, module);
        NoteAnchor(first);

        if (first._isLineend)
        {
            return line;
        }

        Token? token;
        while ((token = cursor.GetNext()) != null)
        {
            string add = Text(token, module);
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

    private string Text(Token token, string? module) =>
        token.GetString()
        + (
            token.VariableIndex >= 0 ? _scopes.VariableSuffix(token.VariableIndex, module)
            : token.FuncIndex >= 0 ? _scopes.FuncSuffix(token.FuncIndex, module)
            : ""
        );

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
        if (_moduleOpens.TryGetValue(token.Id, out SourceLine moduleOpen))
        {
            return moduleOpen;
        }

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
