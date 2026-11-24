using System;
using System.IO;
using HspDecompiler.Core.Ax2.Data;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax2.Reading;

// Reads an HSP 2 object file into an AxData: the 80-byte header, each segment it points at, and
// the label, DLL, #func and #deffunc tables.
//
// HSP 2 のオブジェクトファイルを AxData に読む。80 バイトのヘッダー、それが指す各セグメント、
// ラベル・DLL・#func・#deffunc のテーブル。
internal static class Ax2ImageReader
{
    private const int HeaderSize = 80;
    private const int LabelIndexBaseOffset = 0x1000;

    internal static AxData Read(Stream stream)
    {
        long start = stream.Position;
        Header header = ReadHeader(stream);
        byte[] tokenData;
        byte[] dllData;
        byte[] funcData;
        byte[] deffuncData;
        byte[] moduleData;
        byte[] labelData;
        byte[] stringData;
        try
        {
            tokenData = ReadSegment(stream, start + header.ScriptOffset, header.ScriptByte);
            dllData = ReadSegment(stream, start + header.DllOffset, header.DllByte);
            funcData = ReadSegment(stream, start + header.FuncOffset, header.FuncByte);
            deffuncData = ReadSegment(stream, start + header.DeffuncOffset, header.DeffuncByte);
            moduleData = ReadSegment(stream, start + header.ModuleOffset, header.ModuleByte);
            labelData = ReadSegment(stream, start + header.LabelOffset, header.LabelByte);
            stringData = ReadSegment(stream, start + header.TextOffset, header.TextByte);
        }
        catch (Exception e)
        {
            throw new HspDecoderException("AxHeader", Strings.UnexpectedErrorDuringStreamRead, e);
        }

        stream.Seek(start, SeekOrigin.Begin);
        Label[] labels = ReadLabels(header, labelData);
        return new AxData(
            header,
            labels,
            ReadDlls(header, dllData),
            ReadFuncs(header, funcData, stringData),
            ReadDeffuncs(header, deffuncData, stringData, labels),
            tokenData,
            stringData
        )
        {
            Modules = ReadModules(header, moduleData),
        };
    }

    private static Header ReadHeader(Stream stream)
    {
        byte[] headerBuffer = new byte[HeaderSize];
        if (stream.Read(headerBuffer, 0, HeaderSize) < HeaderSize)
        {
            throw new HspDecoderException("AxData", Strings.FileHeaderMissing);
        }

        int[] words = new int[HeaderSize / 4];
        for (int i = 0; i < words.Length; i++)
        {
            words[i] = BitConverter.ToInt32(headerBuffer, i * 4);
        }

        Header? header;
        try
        {
            header = Header.FromIntArray(words);
        }
        catch (Exception e)
        {
            throw new HspDecoderException(
                "AxHeader",
                Strings.UnexpectedErrorDuringHeaderAnalysis,
                e
            );
        }

        return header ?? throw new HspDecoderException("AxHeader", Strings.HeaderAnalysisFailed);
    }

    private static byte[] ReadSegment(Stream stream, long position, int size)
    {
        byte[] segment = new byte[size];
        stream.Seek(position, SeekOrigin.Begin);
        stream.ReadExactly(segment, 0, size);
        return segment;
    }

    private static Label[] ReadLabels(Header header, byte[] labelData)
    {
        var labels = new Label[header.LabelCount];
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i] = new Label(i, BitConverter.ToInt32(labelData, i * 4));
        }

        return labels;
    }

    private static Dll[] ReadDlls(Header header, byte[] dllData)
    {
        var dlls = new Dll[header.DllCount];
        for (int i = 0; i < dlls.Length; i++)
        {
            dlls[i].Name = AxData.ReadString(4 + (i * 24), dllData);
        }

        return dlls;
    }

    private static Func[] ReadFuncs(Header header, byte[] funcData, byte[] stringData)
    {
        var funcs = new Func[header.FuncCount];
        for (int i = 0; i < funcs.Length; i++)
        {
            int offset = i * 16;
            funcs[i].DllIndex = BitConverter.ToInt16(funcData, offset);
            funcs[i].HikiType = BitConverter.ToInt16(funcData, offset + 4);
            int nameOffset = BitConverter.ToInt32(funcData, offset + 8);
            funcs[i].NameOffset = nameOffset;
            funcs[i].Name = AxData.ReadString(nameOffset, stringData);
        }

        return funcs;
    }

    // hspcmp 2.5 and 2.55 write one 24-byte record per #module: a word hspcmp always sets to 1,
    // then the module name in 20 bytes. Later releases write none.
    //
    // hspcmp 2.5 と 2.55 は #module ごとに 24 バイトの記録を書く。hspcmp が常に 1 にするワードと、
    // 20 バイトのモジュール名である。後のリリースは書かない。
    private static string[] ReadModules(Header header, byte[] moduleData)
    {
        string[] modules = new string[header.ModuleCount];
        for (int i = 0; i < modules.Length; i++)
        {
            modules[i] = AxData.ReadString(4 + (i * 24), moduleData);
        }

        return modules;
    }

    // Each #deffunc entry points at the label its code starts at, which then prints as the
    // #deffunc line.
    //
    // 各 #deffunc の項目はコードが始まるラベルを指し、そのラベルが #deffunc の行として出力される。
    private static Deffunc[] ReadDeffuncs(
        Header header,
        byte[] deffuncData,
        byte[] stringData,
        Label[] labels
    )
    {
        var deffuncs = new Deffunc[header.DeffuncCount];
        for (int i = 0; i < deffuncs.Length; i++)
        {
            int offset = i * 16;
            int labelIndex = BitConverter.ToInt32(deffuncData, offset) - LabelIndexBaseOffset;
            deffuncs[i].HikiType = BitConverter.ToInt16(deffuncData, offset + 4);
            deffuncs[i].HikiCount = BitConverter.ToInt16(deffuncData, offset + 6);
            int nameOffset = BitConverter.ToInt32(deffuncData, offset + 8);
            deffuncs[i].NameOffset = nameOffset;
            deffuncs[i].Name = AxData.ReadString(nameOffset, stringData);
            labels[labelIndex].Deffunc = i;
            labels[labelIndex].Name = deffuncs[i].ToString();
        }

        return deffuncs;
    }
}
