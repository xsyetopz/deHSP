using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax2.Data;

internal class AxData
{
    private const int LabelIndexBaseOffset = 0x1000;

    private Header? _header;
    private Label[]? _labels;
    private Dll[]? _dlls;
    private Func[]? _funcs;
    private Deffunc[]? _deffuncs;
    private Module[]? _modules;

    private byte[]? _labelData;
    private byte[]? _dllData;
    private byte[]? _funcData;
    private byte[]? _deffuncData;
    private byte[]? _tokenData;

    internal byte[] TokenData => _tokenData!;

    private byte[]? _stringData;

    private void ReadData(Stream stream)
    {
        long startPosition = stream.Position;
        byte[] headerBuffer = new byte[80];
        if (stream.Read(headerBuffer, 0, 80) < 80)
        {
            throw new HspDecoderException("AxData", Strings.FileHeaderMissing);
        }

        int[] buffer = new int[20];
        for (int i = 0; i < 20; i++)
        {
            buffer[i] = BitConverter.ToInt32(headerBuffer, i * 4);
        }

        try
        {
            _header = Header.FromIntArray(buffer);
        }
        catch (Exception e)
        {
            throw new HspDecoderException(
                "AxHeader",
                Strings.UnexpectedErrorDuringHeaderAnalysis,
                e
            );
        }

        if (_header == null)
        {
            throw new HspDecoderException("AxHeader", Strings.HeaderAnalysisFailed);
        }

        try
        {
            Header head = _header;
            _tokenData = ReadSegment(stream, startPosition + head.ScriptOffset, head.ScriptByte);
            _dllData = ReadSegment(stream, startPosition + head.DllOffset, head.DllByte);
            _funcData = ReadSegment(stream, startPosition + head.FuncOffset, head.FuncByte);
            _deffuncData = ReadSegment(
                stream,
                startPosition + head.DeffuncOffset,
                head.DeffuncByte
            );
            // The decompiler ignores #module data, but reading it still rejects a truncated file.
            //
            // デコンパイラーは #module のデータを使わないが、読むことで途中で切れたファイルを弾く。
            _ = ReadSegment(stream, startPosition + head.ModuleOffset, head.ModuleByte);
            _labelData = ReadSegment(stream, startPosition + head.LabelOffset, head.LabelByte);
            _stringData = ReadSegment(stream, startPosition + head.TextOffset, head.TextByte);
        }
        catch (Exception e)
        {
            throw new HspDecoderException("AxHeader", Strings.UnexpectedErrorDuringStreamRead, e);
        }

        stream.Seek(startPosition, SeekOrigin.Begin);
    }

    private static byte[] ReadSegment(Stream stream, long position, int size)
    {
        byte[] segment = new byte[size];
        stream.Seek(position, SeekOrigin.Begin);
        stream.ReadExactly(segment, 0, size);
        return segment;
    }

    #region create

    internal static AxData FromStream(Stream stream)
    {
        var data = new AxData();
        data.ReadData(stream);
        return data;
    }

    #endregion

    #region read

    internal string GetString(int offset) => ReadString(offset, _stringData!);

    private string ReadString(int offset) => ReadString(offset, _stringData!);

    private static string ReadString(int offset, byte[] dumpData)
    {
        System.Text.Encoding encode = Encoding.ShiftJisHelper.Encoding;
        var buffer = new List<byte>();
        byte token;
        while (offset < dumpData.Length)
        {
            token = dumpData[offset];
            offset++;
            if (token == 0)
            {
                break;
            }

            buffer.Add(token);
        }

        if (buffer.Count == 0)
        {
            return "";
        }

        byte[] bytes = new byte[buffer.Count];
        buffer.CopyTo(bytes);
        return encode.GetString(bytes);
    }

    private void ReadLabels()
    {
        _labels = new Label[_header!.LabelCount];
        for (int i = 0; i < _header.LabelCount; i++)
        {
            int offset = i * 4;
            _labels[i] = new Label(i, BitConverter.ToInt32(_labelData!, offset));
        }
    }

    private void ReadDlls()
    {
        _dlls = new Dll[_header!.DllCount];

        for (int i = 0; i < _header.DllCount; i++)
        {
            int offset = 4 + (i * 24);
            _dlls[i].Name = ReadString(offset, _dllData!);
        }
    }

    private void ReadFuncs()
    {
        _funcs = new Func[_header!.FuncCount];
        for (int i = 0; i < _header.FuncCount; i++)
        {
            int offset = i * 16;
            _funcs[i].DllIndex = BitConverter.ToInt16(_funcData!, offset);
            offset += 4;
            _funcs[i].HikiType = BitConverter.ToInt16(_funcData!, offset);
            offset += 4;
            int funcnameOffset = BitConverter.ToInt32(_funcData!, offset);
            _funcs[i].NameOffset = funcnameOffset;
            _funcs[i].Name = ReadString(funcnameOffset);
        }
    }

    private void ReadModules()
    {
        if (_header!.ModuleCount == 0)
        {
            return;
        }

        _modules = new Module[_header.ModuleCount];

        for (int i = 0; i < _header.ModuleCount; i++)
        {
            int offset = 4 + (i * 24);
            // HSP 2.x stores module names in the DLL segment.
            //
            // HSP 2.x はモジュール名を DLL セグメントに格納する。
            _modules[i].Name = ReadString(offset, _dllData!);
        }
    }

    private void ReadDeffuncs()
    {
        _deffuncs = new Deffunc[_header!.DeffuncCount];

        for (int i = 0; i < _header.DeffuncCount; i++)
        {
            int offset = i * 16;
            int labelIndex = BitConverter.ToInt32(_deffuncData!, offset) - LabelIndexBaseOffset;
            _labels![labelIndex].Deffunc = i;

            offset += 4;
            _deffuncs[i].HikiType = BitConverter.ToInt16(_deffuncData!, offset);
            offset += 2;
            _deffuncs[i].HikiCount = BitConverter.ToInt16(_deffuncData!, offset);
            offset += 2;
            int deffuncnameOffset = BitConverter.ToInt32(_deffuncData!, offset);
            _deffuncs[i].NameOffset = deffuncnameOffset;
            _deffuncs[i].Name = ReadString(deffuncnameOffset);
            _labels[labelIndex].Name = _deffuncs[i].ToString();
        }
    }

    #endregion

    private readonly List<string> _undefs = [];
    private readonly List<SourceLine> _lines = [];
    private readonly HashSet<int> _referencedText = [];
    private readonly HashSet<string> _shadowedBuiltins = new(StringComparer.Ordinal);
    private int _lineAnchor = -1;

    internal void Decompile()
    {
        _tabNo = 1;
        _ifEnd.Clear();

        var cursor = new TokenCursor(this);
        _lines.Clear();
        _undefs.Clear();
        _referencedText.Clear();

        ReadLabels();
        ReadDlls();
        ReadFuncs();
        ReadModules();
        ReadDeffuncs();

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

    // HSP 2.61 lets a #deffunc replace a built-in after #undef; the original stays callable
    // as name@hsp, so both the #undef and the @hsp suffix are restored.
    //
    // HSP 2.61 では #undef の後に #deffunc で組み込み命令を置き換えられる。元の命令は
    // name@hsp で呼べるため、#undef と @hsp の両方を復元する。
    private void MarkShadowedBuiltins()
    {
        _shadowedBuiltins.Clear();
        foreach (Deffunc deffunc in _deffuncs ?? [])
        {
            if (Token.IsBuiltinName(deffunc.Name) && _shadowedBuiltins.Add(deffunc.Name))
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
                    _labels![token.LabelIndex].LoadCount += 1;
                }

                if (token._hiddenLabelIndex != -1)
                {
                    _labels![token._hiddenLabelIndex].IsCompilerGenerated = true;
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
        int tokenCount = TokenData.Length / 2;
        for (int i = 0; i < _labels!.Length; i++)
        {
            Label label = _labels[i];
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
        Func[] funcs = _funcs ?? [];
        Dll[] dlls = _dlls ?? [];
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
        var switches = new List<FileSwitch>();
        int offset = 0;
        while (offset < _stringData!.Length)
        {
            int end = Array.IndexOf(_stringData, (byte)0, offset);
            if (end < 0)
            {
                end = _stringData.Length;
            }

            if (!_referencedText.Contains(offset))
            {
                string text = ReadString(offset);
                if (IncludeLayout.IsSourceFileName(text))
                {
                    switches.Add(new FileSwitch(offset, text));
                }
            }

            offset = end + 1;
        }

        return switches;
    }

    internal DecodedSource BuildSource(string? mainFileName)
    {
        List<SourceLine> declarations = GetDeclarations();
        List<SourceLine> merged = IncludeLayout.Merge(_lines, declarations);
        DecodedSource source = IncludeLayout.Split(merged, GetFileSwitches(), mainFileName);
        source.Main.InsertRange(0, _undefs);
        return source;
    }

    private static string GetTab(int tab)
    {
        Debug.Assert(tab >= 0);
        return new string('\t', tab);
    }

    private int _tabNo = 1;
    private readonly List<int> _ifEnd = [];

    private void EmitLabel(Token token)
    {
        for (int i = 0; i < _labels!.Length; i++)
        {
            if (!_labels[i].Enabled)
            {
                continue;
            }

            if (token.Id == _labels[i].TokenIndex)
            {
                int deffunc = _labels[i].Deffunc;
                int anchor =
                    (deffunc >= 0) && (_deffuncs != null) ? _deffuncs[deffunc].NameOffset : -1;
                _lines.Add(new SourceLine(_labels[i].Name, anchor));
                _labels[i].Enabled = false;
            }
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

    internal string? GetDeffuncName(int index) =>
        _deffuncs == null || (index >= _deffuncs.Length) || (index < 0)
            ? null
            : _deffuncs[index].Name;

    internal string? GetFuncName(int index) =>
        _funcs == null || (index >= _funcs.Length) || (index < 0) ? null : _funcs[index].Name;

    internal bool IsShadowedBuiltin(string name) => _shadowedBuiltins.Contains(name);
}
