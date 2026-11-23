using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HspDecompiler.Core.Ax3.Data.Analyzer;
using HspDecompiler.Core.Ax3.Data.PP;
using HspDecompiler.Core.Exceptions;
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

    // Record markers of the debug segment, as hspcmp's CToken::PutDI writes them.
    // ---
    // hspcmp の CToken::PutDI が書き出すデバッグセグメントのレコード識別子。
    private const byte DebugMarkerBigOffset = 252;
    private const byte DebugMarkerVariable = 253;
    private const byte DebugMarkerFileName = 254;
    private const byte DebugMarkerEnd = 255;

    private AxHeader? _header;
    private readonly TokenCollection _tokens = new();
    private List<Label> _labels = [];
    private readonly List<Usedll> _dlls = [];
    private readonly List<Function> _functions = [];
    private readonly List<Param> _functionParams = [];
    private readonly List<PlugIn> _plugIns = [];
    private Runtime? _runtime;
    private readonly List<Function> _modules = [];
    private readonly List<string> _variableName = [];

    internal List<Function> Modules => _modules;

    internal List<PlugIn> PlugIns => _plugIns;

    internal Runtime? Runtime => _runtime;

    internal AxHeader? Header => _header;
    internal TokenCollection Tokens => _tokens;
    internal List<Usedll> Usedlls => _dlls;

    // Literal-data offset and code offset of every string and double literal, in code order.
    // ---
    // コード中の文字列・実数リテラルごとのリテラルデータ位置とコード位置(コード順)。
    internal List<(int DataOffset, int TokenOffset)> LiteralAnchors { get; } = [];

    internal bool HasSingleItemFlags { get; set; }
    internal List<Label> Labels => _labels;
    internal List<Function> Functions => _functions;
    internal List<Param> FunctionParams => _functionParams;

    internal Label GetLabel(int index) => Labels[index];

    internal Function? GetUserFunction(int index) =>
        index < 0 ? null
        : index >= _functions.Count ? null
        : _functions[index];

    internal Function? GetDllFunction(int index) =>
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

    internal bool HasVariableNames => _variableName.Count != 0;

    internal string? GetVariableName(int index) =>
        index < 0 ? null
        : index >= _variableName.Count ? null
        : _variableName[index];

    internal Cmd? AddCmd(int pluginIndex, int methodIndex) =>
        pluginIndex < 0 ? null
        : pluginIndex >= _plugIns.Count ? null
        : _plugIns[pluginIndex].AddCmd(methodIndex);

    internal string ReadString(int offset, int max_count)
    {
        long seekOffset = _seekOrigin + offset;
        long nowPosition = _reader!.BaseStream.Position;
        _reader.BaseStream.Seek(seekOffset, SeekOrigin.Begin);
        var chars = new List<char>();
        char token = '\0';
        int count = 0;
        while ((token = _reader.ReadChar()) != '\0')
        {
            switch (token)
            {
                case '\\':
                    chars.Add('\\');
                    chars.Add('\\');
                    break;
                case '\"':
                    chars.Add('\\');
                    chars.Add('\"');
                    break;
                case '\t':
                    chars.Add('\\');
                    chars.Add('t');
                    break;
                case '\n':
                    chars.Add('\\');
                    chars.Add('n');
                    break;
                case '\r':
                    break;
                default:
                    chars.Add(token);
                    break;
            }
            count++;
            if (count >= max_count)
            {
                break;
            }
        }
        char[] arrayChars = new char[chars.Count];
        chars.CopyTo(arrayChars);
        _reader.BaseStream.Seek(nowPosition, SeekOrigin.Begin);
        return new string(arrayChars);
    }

    internal string ReadStringLiteral(int offset) =>
        ReadString((int)(_header!.LiteralStart + offset), (int)(_header.LiteralSize - offset));

    internal double ReadDoubleLiteral(int offset)
    {
        double ret = 0.0;
        long seekOffset = _seekOrigin + _header!.LiteralStart + offset;
        long nowPosition = _reader!.BaseStream.Position;
        _reader.BaseStream.Seek(seekOffset, SeekOrigin.Begin);
        ret = _reader.ReadDouble();
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
        return;
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
            if (runtimeName != null)
            {
                _runtime = new Runtime(runtimeName);
            }
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
                case FunctionType.cfunc:
                case FunctionType.func:
                    dllfuncs.Add(func);
                    break;
                case FunctionType.comfunc:
                    comfuncs.Add(func);
                    break;
                case FunctionType.defcfunc:
                case FunctionType.deffunc:
                case FunctionType.module:
                    if (func.ParentModule != null)
                    {
                        initializer.Add(func);
                    }
                    else
                    {
                        func.SetName(func.DefaultName ?? "");
                        functionNames.Add(
                            (func.DefaultName ?? "").ToLower(
                                System.Globalization.CultureInfo.CurrentCulture
                            )
                        );
                    }
                    break;
                case FunctionType.NULL:
                    break;
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
            if (
                !functionNames.Contains(
                    defName.ToLower(System.Globalization.CultureInfo.CurrentCulture)
                )
            )
            {
                func.SetName(defName);
                functionNames.Add(defName.ToLower(System.Globalization.CultureInfo.CurrentCulture));
                continue;
            }
            string newName = defName;
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
            } while (functionNames.Contains(newName));
            func.SetName(newName);
            functionNames.Add(newName.ToLower(System.Globalization.CultureInfo.CurrentCulture));
        }
    }

    private static void RenameDllFunctions(List<string> functionNames, List<Function> dllfuncs)
    {
        foreach (Function func in dllfuncs)
        {
            string newName = ResolveDllFunctionName(func.DefaultName ?? "", functionNames);
            func.SetName(newName);
            functionNames.Add(newName.ToLower(System.Globalization.CultureInfo.CurrentCulture));
        }
    }

    // Upper bound for OpenProject_dll2..OpenProject_dllN; past it, func_N reads better.
    // ---
    // OpenProject_dll2〜OpenProject_dllN の上限。これを超えるなら func_N の方が読みやすい。
    private const int MaxDllAliasSuffix = 9;

    // The .ax file stores only the DLL export name, not the alias the author wrote, so the
    // alias comes from the export. HSP names ignore case, and wrapper scripts often define
    // "#deffunc openproject" around an "_OpenProject@8" import, so the bare name collides.
    // The candidates, in order: the undecorated name (OpenProject), the export without the
    // stdcall suffix (_OpenProject), OpenProject_dll, OpenProject_dll2 to OpenProject_dll9
    // for repeated imports, and last the numbered func_N.
    // ---
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
            // ---
            // 展開されたモジュールがそれぞれ同じエクスポートをインポートする(3 つの #module に
            // CloseHandle がある等)ため、重複にはエクスポートごとに番号を付ける。
            for (int n = 2; n <= MaxDllAliasSuffix; n++)
            {
                candidates.Add(string.Create(CultureInfo.InvariantCulture, $"{bare}_dll{n}"));
            }
        }

        foreach (string candidate in candidates)
        {
            if (
                IsHspIdentifier(candidate)
                && !takenLowerNames.Contains(
                    candidate.ToLower(System.Globalization.CultureInfo.CurrentCulture)
                )
            )
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
    // ---
    // 序数のエクスポート("#12")や C++ の装飾名("?Fn@@YAXH@Z")は HSP の名前として無効な
    // ため、func_N に回す。
    private static bool IsHspIdentifier(string name) =>
        name.Length > 0
        && name.Length <= 59
        && (char.IsLetter(name[0]) || name[0] == '_')
        && name.All(c => char.IsLetterOrDigit(c) || c == '_');

    private static void RenameComFunctions(List<string> functionNames, List<Function> comfuncs)
    {
        foreach (Function func in comfuncs)
        {
            string newName = "";
            int index = 1;
            do
            {
                newName = string.Format(
                    CultureInfo.InvariantCulture,
                    s_comFunctionNameFormat,
                    index
                );
                index++;
            } while (functionNames.Contains(newName));
            func.SetName(newName);
            functionNames.Add(newName.ToLower(System.Globalization.CultureInfo.CurrentCulture));
        }
    }

    internal void RenameLables()
    {
        if (_labels.Count <= 0)
        {
            return;
        }

        _labels.Sort();
        int keta = ((int)System.Math.Log10(_labels.Count)) + 1;
        var labelFormat = CompositeFormat.Parse(
            "*label_{0:D0" + keta.ToString(CultureInfo.InvariantCulture) + "}"
        );
        for (int i = 0; i < _labels.Count; i++)
        {
            _labels[i].LabelName = string.Format(CultureInfo.InvariantCulture, labelFormat, i);
        }
        return;
    }

    private bool ReadDebugInfo()
    {
        for (uint i = 0; i < _header!.DebugSize; i++)
        {
            long offset = _seekOrigin + _header.DebugStart + i;
            _reader!.BaseStream.Seek(offset, SeekOrigin.Begin);

            switch (_reader.ReadByte())
            {
                case DebugMarkerBigOffset:
                    i += 2;
                    break;
                case DebugMarkerVariable:
                    int literalOffset =
                        _reader.ReadByte() ^ (_reader.ReadByte() << 8) ^ (_reader.ReadByte() << 16);
                    _variableName.Add(ReadStringLiteral(literalOffset));
                    i += 5;
                    break;
                case DebugMarkerFileName:
                    i += 5;
                    break;
                case DebugMarkerEnd:
                    return true;
                default:
                    break;
            }
        }
        return false;
    }
}
