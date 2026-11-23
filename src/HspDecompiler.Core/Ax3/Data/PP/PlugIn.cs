using System.Collections.Generic;
using System.IO;
using System.Text;
using HspDecompiler.Core.Ax3.Reading;

namespace HspDecompiler.Core.Ax3.Data.PP;

internal class PlugIn : Preprocessor
{
    private PlugIn(int index)
        : base(index) { }

    private string? _dllName;
    private string? _exportName;

    internal static PlugIn FromBinaryReader(Ax3ImageReader image, int index)
    {
        BinaryReader reader = image.Reader;
        _ = reader.ReadInt32();
        var ret = new PlugIn(index);
        int dllNameOffset = reader.ReadInt32();
        int exportNameOffset = reader.ReadInt32();
        // HPIDAT.p_libptr, a library handle the runtime fills in.
        //
        // HPIDAT.p_libptr。ランタイムが埋めるライブラリのハンドル。
        _ = reader.ReadInt32();
        ret._dllName = image.ReadStringLiteral(dllNameOffset);
        ret._exportName = image.ReadStringLiteral(exportNameOffset);
        return ret;
    }

    private readonly Dictionary<int, Cmd> _cmds = [];
    private int _extendedTypeCount;

    internal int ExtendedTypeCount
    {
        get => _extendedTypeCount;
        set => _extendedTypeCount = value;
    }

    internal Cmd AddCmd(int methodIndex)
    {
        if (_cmds.TryGetValue(methodIndex, out Cmd? cmd))
        {
            return cmd;
        }

        cmd = new Cmd(_index, methodIndex);
        _cmds.Add(methodIndex, cmd);
        return cmd;
    }

    internal Dictionary<int, Cmd> GetCmds() => _cmds;

    public override string ToString()
    {
        var strbd = new StringBuilder();
        strbd.Append("#regcmd");
        strbd.Append(' ');
        strbd.Append('"');
        strbd.Append(_exportName);
        strbd.Append('"');
        strbd.Append(',');
        strbd.Append(' ');
        strbd.Append('"');
        strbd.Append(_dllName);
        strbd.Append('"');
        if (_extendedTypeCount != 0)
        {
            strbd.Append(',');
            strbd.Append(' ');
            strbd.Append(_extendedTypeCount);
        }

        return strbd.ToString();
    }
}
