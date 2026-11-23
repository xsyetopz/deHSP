using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
    private const int FuncIndexOldDll = -5;
    private const int FuncIndexComfunc = -7;

    // A DLL entry keeps STRUCTDAT_OT flags in the label index field: #func sets the statement
    // bit (hspcmp 3.7 and 3.8β2 also set the function bit), #cfunc only the function bit, and
    // onexit adds the cleanup bit.
    //
    // DLL のエントリはラベル番号欄に STRUCTDAT_OT のフラグを持つ。#func は文のビットを立て
    // （hspcmp 3.7 と 3.8β2 は関数のビットも立てる）、#cfunc は関数のビットだけ、onexit は後始末の
    // ビットを足す。
    private const int OtCleanup = 0x01;
    private const int OtStatement = 0x02;
    private const int OtFunction = 0x04;

    private Function(int index)
        : base(index) { }

    private int _dllIndex;
    private ModuleScope? _scope;
    private int _functionIndex;
    private List<Param> _functionParams = [];

    internal IReadOnlyList<Param> Parameters => _functionParams;
    private int _strIndex;
    private int _labelIndex;
    private int _flags;

    internal static Function FromBinaryReader(BinaryReader reader, AxData parent, int index)
    {
        var ret = new Function(index)
        {
            _scope = parent.Scope,
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

    // Set when a restored common #include declares this import, so the source does not.
    //
    // 復元した共通ファイルの #include がこのインポートを宣言する時に立て、ソースでは宣言しない。
    internal bool DeclaredByInclude { get; set; }

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

                if ((_labelIndex & OtStatement) != 0)
                {
                    return FunctionType.Func;
                }

                if ((_labelIndex & OtFunction) != 0)
                {
                    return FunctionType.CFunc;
                }
            }
            return FunctionType.None;
        }
    }
    internal FunctionFlags Flags =>
        (_flags == 1) && (_dllIndex == DllIndexDeffunc) ? FunctionFlags.OnExit
        : IsDllFunction && ((_labelIndex & OtCleanup) != 0) ? FunctionFlags.OnExit
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
                strBld.Append(Type == FunctionType.DefCFunc ? "#modcfunc " : "#modfunc ");
                strBld.Append(DeclaredName(true));
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
                    strBld.Append(_functionParams[i].ParamName);
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

    // A declaration hspcmp marked as the old form gets the old form back: the numeric type code
    // where the parameters fit it, otherwise the bare name that hspcmp 3.7 and earlier decorate
    // as _name@16. Either spelling sets the mark again.
    //
    // hspcmp が古い形式の印を付けた宣言は古い形式に戻す。引数が合えば数値の型コード、合わなければ
    // hspcmp 3.7 以前が _name@16 に飾る裸の名前を使う。どちらの書き方でも印が付き直す。
    private string ImportText()
    {
        string quotedName = "\"" + _defaultName + "\"";
        return _functionIndex != FuncIndexOldDll ? quotedName + ParamsText(0)
            : OldFuncTypeCode.TryEncode(_functionParams, out int code)
                ? quotedName + " " + code.ToString(CultureInfo.InvariantCulture)
            : (TryUndecorate(_defaultName, out string bareName) ? bareName : quotedName)
                + ParamsText(0);
    }

    private static bool TryUndecorate(string? name, out string bareName)
    {
        const string prefix = "_";
        const string suffix = "@16";
        bareName = string.Empty;
        if (
            name is null
            || name.Length <= prefix.Length + suffix.Length
            || !name.StartsWith(prefix, StringComparison.Ordinal)
            || !name.EndsWith(suffix, StringComparison.Ordinal)
        )
        {
            return false;
        }

        string inner = name[prefix.Length..^suffix.Length];
        if (
            char.IsAsciiDigit(inner[0]) || !inner.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
        )
        {
            return false;
        }

        bareName = inner;
        return true;
    }

    private string OnExit(string text) =>
        (Flags & FunctionFlags.OnExit) == FunctionFlags.OnExit ? text : string.Empty;

    // A function declared with "local" inside a module is named "name@module"; the
    // declaration inside the module spells it "local name".
    //
    // モジュール内で "local" 付きで宣言した関数の名前は "名前@モジュール" であり、モジュール内
    // の宣言では "local 名前" と書く。
    private string? DeclaredName(bool inModule)
    {
        string? name = FunctionName;
        int at = name?.IndexOf('@', StringComparison.Ordinal) ?? -1;
        return inModule && at >= 0 ? "local " + name![..at] : name;
    }

    // An import declared inside a module would otherwise be local to it.
    //
    // モジュール内で宣言したインポートは、指定しなければそのモジュールのローカルになる。
    private static string Global(bool inModule) => inModule ? "global " : string.Empty;

    private bool IsMethod => _functionParams.Count != 0 && _functionParams[0].IsModuleType;

    // With useModuleStyle the declaration sits inside a rebuilt #module block.
    //
    // useModuleStyle の時、宣言は再構築した #module ブロックの中にある。
    internal string ToString(bool useModuleStyle) =>
        Type switch
        {
            FunctionType.Module => ModuleToString(useModuleStyle),
            FunctionType.DefFunc or FunctionType.DefCFunc when useModuleStyle && IsMethod =>
                ModFunctionToString(),
            FunctionType.DefCFunc => "#defcfunc " + DeclaredName(useModuleStyle) + ParamsText(0),
            FunctionType.DefFunc => "#deffunc "
                + DeclaredName(useModuleStyle)
                + OnExit(" onexit")
                + ParamsText(0),
            FunctionType.Func => "#func "
                + Global(useModuleStyle)
                + FunctionName
                + " "
                + OnExit("onexit ")
                + ImportText(),
            FunctionType.CFunc => "#cfunc "
                + Global(useModuleStyle)
                + FunctionName
                + " "
                + OnExit("onexit ")
                + ImportText(),
            FunctionType.ComFunc => "#comfunc "
                + FunctionName
                + " "
                + _labelIndex.ToString(CultureInfo.InvariantCulture)
                + ParamsText(1),
            FunctionType.None => ParamsText(0),
            _ => "/*#deffunc?*/",
        };

    public override string ToString() => ToString(_scope?.Current != null);
}
