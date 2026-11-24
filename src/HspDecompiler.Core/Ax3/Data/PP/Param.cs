using System.Globalization;
using System.IO;
using System.Text;

namespace HspDecompiler.Core.Ax3.Data.PP;

internal class Param : Preprocessor
{
    // Keeps an unknown parameter type visible in the output instead of dropping it.
    //
    // 未知の引数型を捨てずに出力へ残すためのコメント。
    private const string InvalidTypeCommentPrefix = "/*不正な型 ";

    private Param(int paramIndex)
        : base(paramIndex) { }

    private string _paramTypeName = "NULL";

    internal int Index => _index;
    private short _deffuncIndex;

    internal static Param FromBinaryReader(BinaryReader reader, AxData parent, int index)
    {
        var ret = new Param(index) { _paramType = reader.ReadUInt16() };
        ret._paramTypeName = !parent.Dictionary!.ParamLookUp(ret._paramType, out string? lookupName)
            ? "NULL"
            : lookupName ?? "NULL";

        ret._deffuncIndex = reader.ReadInt16();
        // STRUCTPRM.offset, the parameter position on the stack, which the parameter order
        // already gives the source.
        //
        // STRUCTPRM.offset。スタック上の引数の位置で、ソースには引数の並び順から分かる。
        _ = reader.ReadInt32();

        return ret;
    }

    private bool _paramNameIsUsed;
    private System.UInt16 _paramType;
    private Function? _module;

    internal Function? Module => _module;

    internal short TypeCode => unchecked((short)_paramType);

    private bool _isStructParameter;

    internal void SetFunction(AxData parent)
    {
        if (_deffuncIndex < 0)
        {
            return;
        }

        _module = parent.GetFunction(_deffuncIndex);
        if (_module == null)
        {
            return;
        }

        if (_module.IsModuleFunction)
        {
            if (IsModuleType)
            {
                _nameFormatter = _module.FunctionName ?? "prm_{0}";
            }
            else
            {
                _isStructParameter = true;
            }
        }
    }

    internal bool ParamNameIsUsed
    {
        get => _paramNameIsUsed;
        set => _paramNameIsUsed = value;
    }

    private string _nameFormatter = "prm_{0}";

    // The name the debug segment recorded; a module-type parameter keeps the module name.
    //
    // デバッグセグメントに記録された名前。モジュール型の引数はモジュール名のままにする。
    internal string? DebugName { get; set; }

    private string? SourceName => IsModuleType ? null : DebugName;

    internal string ParamName
    {
        get
        {
            if (SourceName != null)
            {
                return SourceName;
            }

            if (_isStructParameter)
            {
                var strbd = new StringBuilder();
                strbd.Append(_module!.FunctionName);
                strbd.Append('_');
                strbd.Append(string.Format(CultureInfo.InvariantCulture, _nameFormatter, _index));
                return strbd.ToString();
            }
            return string.Format(CultureInfo.InvariantCulture, _nameFormatter, _index);
        }
    }

    internal string ToString(bool forceNamed, bool removeType, bool localToVar)
    {
        var strbd = new StringBuilder();
        if (!removeType)
        {
            if (_paramTypeName == "NULL")
            {
                strbd.Append(InvalidTypeCommentPrefix);
                strbd.Append(_paramType.ToString("X04", CultureInfo.InvariantCulture));
                strbd.Append("*/");
            }
            else if (
                (localToVar) && (_paramTypeName.Equals("local", System.StringComparison.Ordinal))
            )
            {
                strbd.Append("var");
            }
            else
            {
                strbd.Append(_paramTypeName);
            }
        }
        if (forceNamed || _paramNameIsUsed || IsModuleType || SourceName != null)
        {
            if (strbd.Length > 0)
            {
                strbd.Append(' ');
            }

            strbd.Append(
                SourceName ?? string.Format(CultureInfo.InvariantCulture, _nameFormatter, _index)
            );
        }
        return strbd.ToString();
    }

    public override string ToString() => ToString(false, false, false);

    internal bool IsModuleType =>
        _paramTypeName switch
        {
            "modvar" or "modinit" or "modterm" or "struct" => true,
            _ => false,
        };

    // The tag a module's struct opens with. Code names it only where it names the module, as in
    // "newmod v, name".
    //
    // モジュールの構造体の先頭にあるタグ。コードがこれを指すのは "newmod v, 名前" のように
    // モジュールの名前を書く所だけである。
    internal bool IsStructTag => _paramTypeName == "struct";
}
