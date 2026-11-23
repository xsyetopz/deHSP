using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace HspDecompiler.Core.Ax3.Data.PP;

internal enum FunctionType
{
    None = 0x00,
    Func = 0x01,
    CFunc = 0x02,
    DefFunc = 0x03,
    DefCFunc = 0x04,
    ComFunc = 0x05,
    Module = 0x06,
}

[Flags]
internal enum FunctionFlags
{
    None = 0,
    OnExit = 0x01,
}

internal class Function : Preprocessor
{
    // The HSP 3 compiler marks the function kind with these sentinels in the dll, function and
    // label index fields of each finfo entry.
    //
    // HSP 3 コンパイラは finfo 各エントリの dll・関数・ラベル番号欄にこれらの番兵値を書き、関数の
    // 種類を示す。
    private const int DllIndexDeffunc = -1;
    private const int DllIndexDefcfunc = -2;
    private const int DllIndexModule = -3;
    private const int FuncIndexComfunc = -7;
    private const int LabelIndexFunc1 = 2;
    private const int LabelIndexOnExit = 3;
    private const int LabelIndexCfunc = 4;
    private const int LabelIndexFunc2 = 6;

    private Function(int index)
        : base(index) { }

    private int _dllIndex;
    private int _functionIndex;
    private List<Param> _functionParams = [];
    private int _strIndex;
    private int _labelIndex;
    private int _flags;

    internal static Function FromBinaryReader(BinaryReader reader, AxData parent, int index)
    {
        var ret = new Function(index)
        {
            _dllIndex = reader.ReadInt16(),
            _functionIndex = reader.ReadInt16(),
        };

        int paramStart = reader.ReadInt32();
        int paramCount = reader.ReadInt32();
        if (paramCount != 0)
        {
            ret._functionParams = parent.FunctionParams.GetRange(paramStart, paramCount);
        }

        ret._strIndex = reader.ReadInt32();
        if (ret._strIndex >= 0)
        {
            ret._defaultName = parent.ReadStringLiteral(ret._strIndex);
        }

        // STRUCTDAT.size, the stack size of the parameters; the source does not spell it out.
        //
        // STRUCTDAT.size。引数のスタックサイズで、ソースには現れない。
        _ = reader.ReadInt32();
        ret._labelIndex = reader.ReadInt32();

        _ = reader.ReadInt16();
        ret._flags = reader.ReadInt16();
        switch (ret.Type)
        {
            case FunctionType.DefCFunc:
            case FunctionType.DefFunc:
                Label label = parent.GetLabel(ret._labelIndex);
                label.SetFunction(ret);

                ret._label = label;
                break;

            case FunctionType.Func:
            case FunctionType.CFunc:
            case FunctionType.ComFunc:
                Usedll? dll = parent.GetUsedll(ret._dllIndex);
                dll?.AddFunction(ret);

                ret._dll = dll;
                break;
            case FunctionType.Module:
                parent.Modules.Add(ret);
                break;
            case FunctionType.None:
            default:
                break;
        }
        return ret;
    }

    internal bool IsModuleFunction => Type == FunctionType.Module;
    internal bool IsComFunction => Type == FunctionType.ComFunc;
    internal bool IsUserFunction => Type is FunctionType.DefFunc or FunctionType.DefCFunc;

    internal bool IsDllFunction => Type is FunctionType.Func or FunctionType.CFunc;

    internal int Index => _index;

    internal int NameOffset => _strIndex;

    internal Usedll? Dll => _dll;

    internal Label? Label => _label;

    // Code offset of the first token that calls this function, or -1 when nothing calls it.
    //
    // この関数を呼び出す最初のトークンのコードオフセット。呼び出しが無ければ -1。
    internal int FirstUseOffset { get; private set; } = -1;

    internal void MarkUsedAt(int tokenOffset)
    {
        if (FirstUseOffset < 0 || tokenOffset < FirstUseOffset)
        {
            FirstUseOffset = tokenOffset;
        }
    }

    private string? _defaultName;

    internal string? DefaultName => _defaultName;

    internal Function? ParentModule =>
        _functionParams.Count == 0 ? null
        : !_functionParams[0].IsModuleType ? null
        : _functionParams[0].Module;
    private string? _name;
    private Label? _label;
    private Usedll? _dll;

    internal FunctionType Type
    {
        get
        {
            if (_dllIndex == DllIndexDeffunc)
            {
                return FunctionType.DefFunc;
            }

            if (_dllIndex == DllIndexDefcfunc)
            {
                return FunctionType.DefCFunc;
            }

            if (_dllIndex == DllIndexModule)
            {
                return FunctionType.Module;
            }

            if (_dllIndex >= 0)
            {
                if (_functionIndex == FuncIndexComfunc)
                {
                    return FunctionType.ComFunc;
                }

                if (_labelIndex is LabelIndexFunc1 or LabelIndexFunc2)
                {
                    return FunctionType.Func;
                }

                if (_labelIndex == LabelIndexOnExit)
                {
                    return FunctionType.Func;
                }

                if (_labelIndex == LabelIndexCfunc)
                {
                    return FunctionType.CFunc;
                }
            }
            return FunctionType.None;
        }
    }
    internal FunctionFlags Flags =>
        (_flags == 1) && (_dllIndex == DllIndexDeffunc) ? FunctionFlags.OnExit
        : (_dllIndex >= 0) && (_labelIndex == LabelIndexOnExit) ? FunctionFlags.OnExit
        : FunctionFlags.None;

    internal void SetName(string name) => _name = name;

    internal string? FunctionName
    {
        get
        {
            if (_name != null)
            {
                return _name;
            }

            if (_defaultName == null)
            {
                return Type == FunctionType.ComFunc
                    ? "comfunc_" + _index.ToString(CultureInfo.InvariantCulture)
                    : null;
            }
            switch (Type)
            {
                case FunctionType.DefCFunc:
                case FunctionType.DefFunc:
                case FunctionType.Module:
                    return _defaultName;
                case FunctionType.Func:
                case FunctionType.CFunc:
                    if (_name != null)
                    {
                        return _name;
                    }

                    return _defaultName;
                case FunctionType.ComFunc:
                    return "comfunc_" + _index.ToString(CultureInfo.InvariantCulture);
                case FunctionType.None:
                default:
                    break;
            }
            return null;
        }
    }

    private string ModFunctionToString()
    {
        var strBld = new StringBuilder();
        switch (_defaultName)
        {
            case "__init":
                strBld.Append("#modinit");
                break;
            case "__term":
                strBld.Append("#modterm");
                break;
            default:
                strBld.Append("#modfunc");
                strBld.Append(' ');
                strBld.Append(FunctionName);
                break;
        }
        if (_functionParams.Count > 1)
        {
            for (int i = 1; i < _functionParams.Count; i++)
            {
                if (i != 1)
                {
                    strBld.Append(',');
                }

                strBld.Append(' ');
                strBld.Append(_functionParams[i]);
            }
        }
        return strBld.ToString();
    }

    private string ModuleToString(bool useModuleStyle)
    {
        var strBld = new StringBuilder();
        if (useModuleStyle)
        {
            strBld.Append("#module ");
            strBld.Append(FunctionName);
        }
        else
        {
            strBld.Append("#struct ");
            strBld.Append(FunctionName);
        }
        if (_functionParams.Count > 1)
        {
            for (int i = 1; i < _functionParams.Count; i++)
            {
                if (i != 1)
                {
                    strBld.Append(',');
                }

                strBld.Append(' ');
                if (useModuleStyle)
                {
                    strBld.Append(_functionParams[i].ToString(true, true, true));
                }
                else
                {
                    strBld.Append(_functionParams[i].ToString(true, false, true));
                }
            }
        }
        return strBld.ToString();
    }

    private string ParamsText(int paramStart)
    {
        var strBld = new StringBuilder();
        for (int i = paramStart; i < _functionParams.Count; i++)
        {
            strBld.Append(i == paramStart ? " " : ", ");
            strBld.Append(_functionParams[i]);
        }

        return strBld.ToString();
    }

    private string OnExit(string text) =>
        (Flags & FunctionFlags.OnExit) == FunctionFlags.OnExit ? text : string.Empty;

    internal string ToString(bool useModuleStyle) =>
        Type switch
        {
            FunctionType.Module => ModuleToString(useModuleStyle),
            FunctionType.DefFunc
                when useModuleStyle
                    && (_functionParams.Count != 0)
                    && _functionParams[0].IsModuleType => ModFunctionToString(),
            FunctionType.DefCFunc => "#defcfunc " + FunctionName + ParamsText(0),
            FunctionType.DefFunc => "#deffunc " + FunctionName + OnExit(" onexit") + ParamsText(0),
            FunctionType.Func => "#func "
                + FunctionName
                + " "
                + OnExit("onexit ")
                + "\""
                + _defaultName
                + "\""
                + ParamsText(0),
            FunctionType.CFunc => "#cfunc "
                + FunctionName
                + " \""
                + _defaultName
                + "\""
                + ParamsText(0),
            FunctionType.ComFunc => "#comfunc "
                + FunctionName
                + " "
                + _labelIndex.ToString(CultureInfo.InvariantCulture)
                + ParamsText(1),
            FunctionType.None => ParamsText(0),
            _ => "/*#deffunc?*/",
        };

    public override string ToString() => ToString(false);
}
