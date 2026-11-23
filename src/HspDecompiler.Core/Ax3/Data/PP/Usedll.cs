using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HspDecompiler.Core.Ax3.Data.PP;

internal enum UsedllType
{
    None = 0x00,
    UseLib = 0x01,
    UseCom = 0x02,
}

internal sealed class Usedll : Preprocessor
{
    private Usedll(int index)
        : base(index) { }

    private string? _name;
    private string? _clsName;
    private int _type;

    internal static Usedll FromBinaryReader(BinaryReader reader, AxData parent, int index)
    {
        var ret = new Usedll(index) { _type = reader.ReadInt32() };
        int nameOffset = reader.ReadInt32();
        // LIBDAT.hlib, a library handle the runtime fills in.
        //
        // LIBDAT.hlib。ランタイムが埋めるライブラリのハンドル。
        _ = reader.ReadInt32();
        int clsNameOffset = reader.ReadInt32();
        switch (ret.Type)
        {
            case UsedllType.UseCom:
                ret._name = parent.ReadIidCodeLiteral(nameOffset);
                ret._clsName = parent.ReadStringLiteral(clsNameOffset);
                break;
            case UsedllType.UseLib:
                ret._name = parent.ReadStringLiteral(nameOffset);
                break;
            case UsedllType.None:
            default:
                break;
        }

        return ret;
    }

    private readonly List<Function> _functions = [];

    internal UsedllType Type =>
        _type switch
        {
            1 => UsedllType.UseLib,
            4 => UsedllType.UseCom,
            _ => UsedllType.None,
        };

    public override string ToString()
    {
        if (_name == null)
        {
            return @"//#uselib? //dll情報なし";
        }

        var strBld = new StringBuilder();
        switch (Type)
        {
            case UsedllType.UseLib:
                strBld.Append(@"#uselib """);
                strBld.Append(_name);
                strBld.Append('"');
                break;
            case UsedllType.UseCom:
                strBld.Append(@"#usecom");
                if (_functions.Count != 0)
                {
                    strBld.Append(' ');
                    strBld.Append(_functions[0].FunctionName);
                }
                else
                {
                    strBld.Append(' ');
                    strBld.Append("/*関数なし*/");
                }

                strBld.Append(' ');
                strBld.Append('"');
                strBld.Append(_name);
                strBld.Append('"');
                strBld.Append(' ');
                strBld.Append('"');
                strBld.Append(_clsName);
                strBld.Append('"');
                break;
            case UsedllType.None:
            default:
                return @"//#uselib? //非対応の形式";
        }
        return strBld.ToString();
    }

    internal void AddFunction(Function ret) => _functions.Add(ret);

    // A #usecom line itself declares the COM object's first function entry.
    //
    // #usecom 行そのものが COM オブジェクトの最初の関数エントリを宣言する。
    internal bool IsDeclaredByHeader(Function function) =>
        Type == UsedllType.UseCom && _functions.Count != 0 && _functions[0] == function;
}
