using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HspDecompiler.Core.Ax3.Data.Analyzer;
using HspDecompiler.Core.Ax3.Data.PP;
using HspDecompiler.Core.Encoding;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Pipeline;
using HspDecompiler.Core.Releases;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax3.Data;

internal class AxData
{
    private static readonly CompositeFormat s_userFunctionNameFormat = CompositeFormat.Parse(
        "{0}_{1}"
    );

    private static readonly CompositeFormat s_dllFunctionNameFormat = CompositeFormat.Parse(
        "func_{0}"
    );

    private static readonly CompositeFormat s_comFunctionNameFormat = CompositeFormat.Parse(
        "comfunc_{0}"
    );

    private AxHeader? _header;
    private readonly TokenCollection _tokens = new();
    private List<Label> _labels = [];
    private readonly List<Usedll> _dlls = [];
    private readonly List<Function> _functions = [];
    private readonly List<Param> _functionParams = [];
    private readonly List<PlugIn> _plugIns = [];
    private Runtime? _runtime;
    private readonly List<Function> _modules = [];
    private DebugInfo _debugInfo = new();

    internal DebugInfo DebugInfo => _debugInfo;

    internal ModuleScope Scope { get; } = new();

    internal SourceStyle Style { get; init; } = SourceStyle.Default;

    // The releases that could have written this AX; every release until the code is read.
    //
    // この AX を書いた可能性のあるリリース。コードを読むまではすべてのリリース。
    internal ReleaseProfile Release { get; set; } = ReleaseProfile.Unknown;

    internal List<Function> Modules => _modules;

    internal List<PlugIn> PlugIns => _plugIns;

    internal Runtime? Runtime => _runtime;

    internal AxHeader? Header => _header;
    internal TokenCollection Tokens => _tokens;
    internal List<Usedll> Usedlls => _dlls;

    // Literal-data offset and code offset of every string and double literal, in code order.
    //
    // コード中の文字列・実数リテラルごとのリテラルデータ位置とコード位置(コード順)。
    internal List<(int DataOffset, int TokenOffset)> LiteralAnchors { get; } = [];

    internal bool HasSingleItemFlags { get; set; }
    internal List<Label> Labels => _labels;
    internal List<Function> Functions => _functions;
    internal List<Param> FunctionParams => _functionParams;

    internal Label GetLabel(int index) => Labels[index];

    internal Function? GetFunction(int index) =>
        index < 0 ? null
        : index >= _functions.Count ? null
        : _functions[index];

    internal Usedll? GetUsedll(int index) =>
        index < 0 ? null
        : index >= _dlls.Count ? null
        : _dlls[index];

    internal Param? GetParam(int index) =>
        index < 0 ? null
        : index >= _functionParams.Count ? null
        : _functionParams[index];

    internal bool HasVariableNames => _debugInfo.VariableNames.Count != 0;

    internal string? GetVariableName(int index) =>
        index < 0 ? null
        : index >= _debugInfo.VariableNames.Count ? null
        : ScriptStrings.Encoding.GetString(_debugInfo.VariableNames[index]);

    internal string? GetDebugName(Dictionary<int, byte[]> names, int index) =>
        names.TryGetValue(index, out byte[]? name) ? DecodeName(name) : null;

    // Names stored through PutDSBuf keep the source encoding, which is UTF-8 only when the
    // strings are UTF-8 too and the bytes hold valid UTF-8.
    //
    // PutDSBuf で格納された名前はソースのエンコーディングのままで、文字列も UTF-8 であり
    // バイト列が正しい UTF-8 の時だけ UTF-8 になる。
    internal string DecodeName(byte[] name) =>
        ScriptStrings.IsUtf8 && System.Text.Unicode.Utf8.IsValid(name)
            ? ScriptStrings.Encoding.GetString(name)
            : ShiftJisHelper.Encoding.GetString(name);

    internal Cmd? AddCmd(int pluginIndex, int methodIndex) =>
        pluginIndex < 0 ? null
        : pluginIndex >= _plugIns.Count ? null
        : _plugIns[pluginIndex].AddCmd(methodIndex);

    // Reads the NUL-terminated bytes at the offset, at most maxCount of them, so a missing
    // terminator near the end of the file stops at the segment instead of throwing.
    //
    // 指定位置から NUL 終端のバイト列を最大 maxCount バイト読む。ファイル末尾付近で終端が
    // 無くても、例外を投げずにセグメントの終わりで止まる。
    private byte[] ReadBytes(int offset, int maxCount)
    {
        Stream stream = _reader!.BaseStream;
        long nowPosition = stream.Position;
        stream.Seek(_seekOrigin + offset, SeekOrigin.Begin);
        var bytes = new List<byte>();
        int next;
        while (bytes.Count < maxCount && (next = stream.ReadByte()) > 0)
        {
            bytes.Add((byte)next);
        }

        stream.Seek(nowPosition, SeekOrigin.Begin);
        return [.. bytes];
    }

    internal string ReadString(int offset, int maxCount) =>
        ScriptStrings.Escape(ShiftJisHelper.Encoding.GetString(ReadBytes(offset, maxCount)));

    internal string ReadStringLiteral(int offset) =>
        ScriptStrings.Escape(ShiftJisHelper.Encoding.GetString(ReadLiteralBytes(offset)));

    internal byte[] ReadScriptString(int offset) => ScriptStrings.Add(ReadLiteralBytes(offset));

    // hspcmp under optcode stores each non-empty string once and points every later use at the
    // first copy (codegen.cpp PutDSStr, CMembuf::SearchIndexedData). The same text at two
    // offsets therefore shows that the AX was compiled with optcode off.
    //
    // optcode 指定時の hspcmp は空でない文字列を一度だけ格納し、以降の使用は最初の写しを
    // 指す(codegen.cpp の PutDSStr、CMembuf::SearchIndexedData)。同じ文字列が 2 か所に
    // あれば、その AX は optcode を切ってコンパイルされている。
    private readonly Dictionary<string, int> _literalOffsets = [];

    internal bool HasRepeatedStrings { get; private set; }

    private byte[] ReadLiteralBytes(int offset)
    {
        byte[] bytes = ReadBytes(
            (int)(_header!.LiteralStart + offset),
            (int)(_header.LiteralSize - offset)
        );
        if (bytes.Length != 0)
        {
            string key = Convert.ToHexString(bytes);
            if (!_literalOffsets.TryAdd(key, offset) && _literalOffsets[key] != offset)
            {
                HasRepeatedStrings = true;
            }
        }

        return bytes;
    }

    internal ScriptStrings ScriptStrings { get; } = new();

    // Set when the code calls await. OpenHSP's hspcmp then leaves the NOMMTIMER boot bit clear,
    // and sets it otherwise (codegen.cpp hed_autoopt_timer).
    //
    // コードが await を呼ぶと立つ。OpenHSP の hspcmp はその場合 NOMMTIMER 起動ビットを立てず、
    // それ以外では立てる(codegen.cpp の hed_autoopt_timer)。
    internal bool UsesAwait { get; set; }

    internal double ReadDoubleLiteral(int offset)
    {
        long seekOffset = _seekOrigin + _header!.LiteralStart + offset;
        long nowPosition = _reader!.BaseStream.Position;
        _reader.BaseStream.Seek(seekOffset, SeekOrigin.Begin);
        double ret = _reader.ReadDouble();
        _reader.BaseStream.Seek(nowPosition, SeekOrigin.Begin);
        return ret;
    }

    internal string ReadIidCodeLiteral(int offset)
    {
        var strbd = new StringBuilder();
        byte[] buf;
        long seekOffset = _seekOrigin + _header!.LiteralStart + offset;
        long nowPosition = _reader!.BaseStream.Position;
        _reader.BaseStream.Seek(seekOffset, SeekOrigin.Begin);
        buf = _reader.ReadBytes(0x10);
        _reader.BaseStream.Seek(nowPosition, SeekOrigin.Begin);
        strbd.Append('{');
        strbd.Append(buf[0x03].ToString("X02", CultureInfo.InvariantCulture));
        strbd.Append(buf[0x02].ToString("X02", CultureInfo.InvariantCulture));
        strbd.Append(buf[0x01].ToString("X02", CultureInfo.InvariantCulture));
        strbd.Append(buf[0x00].ToString("X02", CultureInfo.InvariantCulture));
        strbd.Append('-');
        strbd.Append(buf[0x05].ToString("X02", CultureInfo.InvariantCulture));
        strbd.Append(buf[0x04].ToString("X02", CultureInfo.InvariantCulture));
        strbd.Append('-');
        strbd.Append(buf[0x07].ToString("X02", CultureInfo.InvariantCulture));
        strbd.Append(buf[0x06].ToString("X02", CultureInfo.InvariantCulture));
        strbd.Append('-');
        strbd.Append(buf[0x08].ToString("X02", CultureInfo.InvariantCulture));
        strbd.Append(buf[0x09].ToString("X02", CultureInfo.InvariantCulture));
        strbd.Append('-');
        strbd.Append(buf[0x0A].ToString("X02", CultureInfo.InvariantCulture));
        strbd.Append(buf[0x0B].ToString("X02", CultureInfo.InvariantCulture));
        strbd.Append(buf[0x0C].ToString("X02", CultureInfo.InvariantCulture));
        strbd.Append(buf[0x0D].ToString("X02", CultureInfo.InvariantCulture));
        strbd.Append(buf[0x0E].ToString("X02", CultureInfo.InvariantCulture));
        strbd.Append(buf[0x0F].ToString("X02", CultureInfo.InvariantCulture));
        strbd.Append('}');
        return strbd.ToString();
    }

    internal void LoadStart(BinaryReader theReader, Hsp3Dictionary? theDictionary)
    {
        if (theReader == null)
        {
            throw new ArgumentNullException(nameof(theReader), Strings.ArgumentNullReader);
        }

        if (theDictionary == null)
        {
            throw new ArgumentNullException(Strings.ArgumentNullDictionary);
        }

        _seekOrigin = theReader.BaseStream.Position;
        _reader = theReader;
        _dictionary = theDictionary;
        _isStarted = true;
    }

    internal void LoadEnd()
    {
        _seekOrigin = -1;
        _reader = null;
        _dictionary = null;
        _isStarted = false;
    }

    private long _seekOrigin;
    private BinaryReader? _reader;
    private Hsp3Dictionary? _dictionary;
    private bool _isStarted;

    internal bool IsStarted => _isStarted;

    internal BinaryReader? Reader => _reader;
    internal long StartOfCode => _header!.CodeStart + _seekOrigin;

    internal Hsp3Dictionary? Dictionary => _dictionary;

    internal void ReadHeader()
    {
        if (!_isStarted)
        {
            throw new InvalidOperationException(Strings.LoadStartNotCalled);
        }

        long streamSize = _reader!.BaseStream.Length - _seekOrigin;
        if (streamSize < AxHeader.HeaderSize)
        {
            throw new HspDecoderException("AxData", Strings.FileHeaderMissing);
        }

        try
        {
            _header = AxHeader.FromBinaryReader(_reader);
        }
        catch (SystemException e)
        {
            throw new HspDecoderException(
                "AxHeader",
                Strings.UnexpectedErrorDuringHeaderAnalysis,
                e
            );
        }
    }

    private void ReadTable<T>(
        List<T> table,
        uint start,
        uint count,
        HeaderDataSize size,
        Func<BinaryReader, AxData, int, T> read
    )
    {
        for (int i = 0; i < count; i++)
        {
            _reader!.BaseStream.Seek(_seekOrigin + start + ((int)size * i), SeekOrigin.Begin);
            table.Add(read(_reader, this, i));
        }
    }

    private void ReadTables(AxHeader header)
    {
        ReadTable(
            _labels,
            header.LabelStart,
            header.LabelCount,
            HeaderDataSize.Label,
            Label.FromBinaryReader
        );
        ReadTable(
            _dlls,
            header.DllStart,
            header.DllCount,
            HeaderDataSize.Dll,
            Usedll.FromBinaryReader
        );
        ReadTable(
            _functionParams,
            header.ParameterStart,
            header.ParameterCount,
            HeaderDataSize.Parameter,
            Param.FromBinaryReader
        );
        ReadTable(
            _functions,
            header.FunctionStart,
            header.FunctionCount,
            HeaderDataSize.Function,
            Function.FromBinaryReader
        );
        ReadTable(
            _plugIns,
            header.PluginStart,
            header.PluginCount,
            HeaderDataSize.Plugin,
            PlugIn.FromBinaryReader
        );
    }

    internal void ReadPreprocessor(Hsp3Dictionary? dictionary)
    {
        if (!_isStarted)
        {
            throw new InvalidOperationException(Strings.LoadStartNotCalled);
        }

        if (_header == null)
        {
            throw new InvalidOperationException(Strings.HeaderNotLoaded);
        }

        if (_header.RuntimeStart != 0)
        {
            string runtimeName = ReadString(
                (int)_header.RuntimeStart,
                (int)(_header.CodeStart - _header.RuntimeStart)
            );
            _runtime = new Runtime(runtimeName);
        }

        ReadTables(_header);
        if ((_header.PluginCount != 0) && (_header.PluginParameterCount != 0))
        {
            _plugIns[0].ExtendedTypeCount = (int)_header.PluginParameterCount;
        }

        foreach (Param param in _functionParams)
        {
            param.SetFunction(this);
        }

        RenameFunctions(dictionary!);

        ReadDebugInfo();
    }

    internal void DeleteInvisibleLabels() => _labels = _labels.FindAll(LabelIsVisible);

    private bool LabelIsVisible(Label label) => label.Visible;

    private void RenameFunctions(Hsp3Dictionary dictionary)
    {
        var functionNames = new List<string>();
        var initializer = new List<Function>();
        var comfuncs = new List<Function>();
        var dllfuncs = new List<Function>();
        functionNames.AddRange(dictionary.GetAllFuncName());
        foreach (Function func in _functions)
        {
            switch (func.Type)
            {
                case FunctionType.CFunc:
                case FunctionType.Func:
                    dllfuncs.Add(func);
                    break;
                case FunctionType.ComFunc:
                    comfuncs.Add(func);
                    break;
                case FunctionType.DefCFunc:
                case FunctionType.DefFunc:
                case FunctionType.Module:
                    if (func.ParentModule != null)
                    {
                        initializer.Add(func);
                    }
                    else
                    {
                        func.SetName(func.DefaultName ?? "");
                        functionNames.Add(NameKey(func.DefaultName ?? ""));
                    }

                    break;
                case FunctionType.None:
                default:
                    break;
            }
        }

        RenameUserFunctions(functionNames, initializer);
        RenameDllFunctions(functionNames, dllfuncs);
        RenameComFunctions(functionNames, comfuncs);
    }

    private static void RenameUserFunctions(List<string> functionNames, List<Function> initializer)
    {
        foreach (Function func in initializer)
        {
            string defName = func.DefaultName ?? "";
            if (!functionNames.Contains(NameKey(defName)))
            {
                func.SetName(defName);
                functionNames.Add(NameKey(defName));
                continue;
            }

            string newName;
            int index = 1;
            do
            {
                newName = string.Format(
                    CultureInfo.InvariantCulture,
                    s_userFunctionNameFormat,
                    defName,
                    index
                );
                index++;
            } while (functionNames.Contains(NameKey(newName)));

            func.SetName(newName);
            functionNames.Add(NameKey(newName));
        }
    }

    private static void RenameDllFunctions(List<string> functionNames, List<Function> dllfuncs)
    {
        foreach (Function func in dllfuncs)
        {
            string newName = ResolveDllFunctionName(func.DefaultName ?? "", functionNames);
            func.SetName(newName);
            functionNames.Add(NameKey(newName));
        }
    }

    // Upper bound for OpenProject_dll2..OpenProject_dllN; past it, func_N reads better.
    //
    // OpenProject_dll2〜OpenProject_dllN の上限。これを超えるなら func_N の方が読みやすい。
    private const int MaxDllAliasSuffix = 9;

    // The .ax file stores only the DLL export name, not the alias the author wrote, so the
    // alias comes from the export. HSP names ignore case, and wrapper scripts often define
    // "#deffunc openproject" around an "_OpenProject@8" import, so the bare name collides.
    // The candidates, in order: the undecorated name (OpenProject), the export without the
    // stdcall suffix (_OpenProject), OpenProject_dll, OpenProject_dll2 to OpenProject_dll9
    // for repeated imports, and last the numbered func_N.
    //
    // .ax ファイルには作者が付けた別名ではなく DLL のエクスポート名しか残らないため、別名は
    // エクスポート名から作る。HSP の名前は大文字と小文字を区別せず、ラッパースクリプトは
    // "_OpenProject@8" のインポートを "#deffunc openproject" で包むことが多いので、素の名前は
    // 衝突しやすい。候補は順に、装飾を外した名前(OpenProject)、stdcall の接尾辞だけを外した
    // 名前(_OpenProject)、OpenProject_dll、重複インポート用の OpenProject_dll2〜
    // OpenProject_dll9、最後に番号付きの func_N。
    internal static string ResolveDllFunctionName(
        string exportName,
        ICollection<string> takenLowerNames
    )
    {
        int atIndex = exportName.IndexOf('@');
        string undecorated = atIndex > 0 ? exportName[..atIndex] : exportName;
        string bare =
            undecorated.StartsWith('_') && undecorated.Length > 1 ? undecorated[1..] : undecorated;

        var candidates = new List<string>();
        if (IsHspIdentifier(bare))
        {
            candidates.Add(bare);
            if (undecorated != bare)
            {
                candidates.Add(undecorated);
            }

            candidates.Add(bare + "_dll");
            // Flattened modules each import the same export (CloseHandle in three #module
            // blocks), so repeats get a number per export.
            //
            // 展開されたモジュールがそれぞれ同じエクスポートをインポートする(3 つの #module に
            // CloseHandle がある等)ため、重複にはエクスポートごとに番号を付ける。
            for (int n = 2; n <= MaxDllAliasSuffix; n++)
            {
                candidates.Add(string.Create(CultureInfo.InvariantCulture, $"{bare}_dll{n}"));
            }
        }

        foreach (string candidate in candidates)
        {
            if (IsHspIdentifier(candidate) && !takenLowerNames.Contains(NameKey(candidate)))
            {
                return candidate;
            }
        }

        int index = 1;
        string numbered;
        do
        {
            numbered = string.Format(CultureInfo.InvariantCulture, s_dllFunctionNameFormat, index);
            index++;
        } while (takenLowerNames.Contains(numbered));

        return numbered;
    }

    // Ordinal exports ("#12") and C++ mangled names ("?Fn@@YAXH@Z") are not valid HSP names,
    // so they fall through to func_N.
    //
    // 序数のエクスポート("#12")や C++ の装飾名("?Fn@@YAXH@Z")は HSP の名前として無効な
    // ため、func_N に回す。
    // HSP names ignore case. The dictionary lowers its names the same invariant way, so both
    // lists compare equal under any UI culture.
    //
    // HSP の名前は大文字と小文字を区別しない。辞書も同じく不変カルチャで小文字にするため、
    // UI のカルチャに関係なく両方の一覧が一致する。
    private static string NameKey(string name) => name.ToLowerInvariant();

    private static bool IsHspIdentifier(string name) =>
        name.Length > 0
        && name.Length <= 59
        && (char.IsLetter(name[0]) || name[0] == '_')
        && name.All(c => char.IsLetterOrDigit(c) || c == '_');

    private static void RenameComFunctions(List<string> functionNames, List<Function> comfuncs)
    {
        foreach (Function func in comfuncs)
        {
            string newName;
            int index = 1;
            do
            {
                newName = string.Format(
                    CultureInfo.InvariantCulture,
                    s_comFunctionNameFormat,
                    index
                );
                index++;
            } while (functionNames.Contains(NameKey(newName)));

            func.SetName(newName);
            functionNames.Add(NameKey(newName));
        }
    }

    // A label the debug segment named keeps that name; the rest are numbered in code order,
    // skipping numbers whose name a recovered label already took.
    //
    // デバッグセグメントが名付けたラベルはその名前を使う。残りはコード順に番号を振り、
    // 復元したラベルが既に使った名前の番号は飛ばす。
    internal void RenameLabels()
    {
        if (_labels.Count <= 0)
        {
            return;
        }

        _labels.Sort();
        Dictionary<int, string> recovered = DebugNames.LabelNames(this, _labels);
        var taken = new HashSet<string>(
            recovered.Values.Select(name => name.ToLowerInvariant()),
            StringComparer.Ordinal
        );
        int keta = ((int)Math.Log10(_labels.Count)) + 1;
        var labelFormat = CompositeFormat.Parse(
            "*label_{0:D0" + keta.ToString(CultureInfo.InvariantCulture) + "}"
        );
        int number = 0;
        foreach (Label label in _labels)
        {
            if (recovered.TryGetValue(label.Index, out string? name))
            {
                label.LabelName = name;
                continue;
            }

            string generated;
            do
            {
                generated = string.Format(CultureInfo.InvariantCulture, labelFormat, number++);
            } while (taken.Contains(generated));

            label.LabelName = generated;
        }
    }

    private void ReadDebugInfo()
    {
        byte[] segment = DebugInfo.ReadSegment(
            _reader!,
            _seekOrigin + _header!.DebugStart,
            (int)_header.DebugSize
        );
        _debugInfo = DebugInfo.Read(segment, ReadScriptString, ReadLiteralBytes);
    }
}
