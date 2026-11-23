using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Ax3.Data.PP;
using HspDecompiler.Core.Encoding;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax3.Reading;

// The byte-level reader of one AX3 image: the header, the object tables, the literal segment and
// the debug segment. It fills an AxData and keeps no model of its own, so the model outlives the
// stream and nothing after reading can seek.
//
// 1 つの AX3 イメージをバイト単位で読む。ヘッダー、オブジェクトテーブル、リテラルセグメント、
// デバッグセグメントを読む。AxData を埋めるだけで自分ではモデルを持たないため、モデルは
// ストリームより長く生き、読み終えた後にシークするものは無い。
internal sealed class Ax3ImageReader
{
    private readonly BinaryReader _reader;
    private readonly long _origin;
    private readonly ScriptStrings _strings;

    // hspcmp under optcode stores each non-empty string once and points every later use at the
    // first copy (codegen.cpp PutDSStr, CMembuf::SearchIndexedData). The same text at two
    // offsets therefore shows that the AX was compiled with optcode off.
    //
    // optcode 指定時の hspcmp は空でない文字列を一度だけ格納し、以降の使用は最初の写しを
    // 指す(codegen.cpp の PutDSStr、CMembuf::SearchIndexedData)。同じ文字列が 2 か所に
    // あれば、その AX は optcode を切ってコンパイルされている。
    private readonly Dictionary<string, int> _literalOffsets = [];

    internal Ax3ImageReader(BinaryReader reader, ScriptStrings strings)
    {
        _reader = reader;
        _origin = reader.BaseStream.Position;
        _strings = strings;
        Header = ReadHeader();
    }

    internal AxHeader Header { get; }

    internal BinaryReader Reader => _reader;

    internal bool HasRepeatedStrings { get; private set; }

    internal long StartOfCode => Header.CodeStart + _origin;

    // The runtime name, the five object tables, the plug-in parameter count and the parameter
    // owners, in the order hspcmp's tables refer to each other.
    //
    // ランタイム名、5 つのオブジェクトテーブル、プラグインの引数の数、引数の持ち主を、hspcmp の
    // テーブルが互いを参照する順に読む。
    internal void ReadTables(AxData data)
    {
        if (Header.RuntimeStart != 0)
        {
            data.Runtime = new Runtime(
                ReadString((int)Header.RuntimeStart, (int)(Header.CodeStart - Header.RuntimeStart))
            );
        }

        ReadObjectTables(data);
        if ((Header.PluginCount != 0) && (Header.PluginParameterCount != 0))
        {
            data.PlugIns[0].ExtendedTypeCount = (int)Header.PluginParameterCount;
        }

        foreach (Param param in data.FunctionParams)
        {
            param.SetFunction(data);
        }
    }

    private void ReadObjectTables(AxData data)
    {
        ReadTable(
            data.Labels,
            Header.LabelStart,
            Header.LabelCount,
            HeaderDataSize.Label,
            data,
            (image, _, i) => Label.FromBinaryReader(image.Reader, i)
        );
        ReadTable(
            data.Usedlls,
            Header.DllStart,
            Header.DllCount,
            HeaderDataSize.Dll,
            data,
            (image, _, i) => Usedll.FromBinaryReader(image, i)
        );
        ReadTable(
            data.FunctionParams,
            Header.ParameterStart,
            Header.ParameterCount,
            HeaderDataSize.Parameter,
            data,
            (image, owner, i) => Param.FromBinaryReader(image.Reader, owner, i)
        );
        ReadTable(
            data.Functions,
            Header.FunctionStart,
            Header.FunctionCount,
            HeaderDataSize.Function,
            data,
            Function.FromBinaryReader
        );
        ReadTable(
            data.PlugIns,
            Header.PluginStart,
            Header.PluginCount,
            HeaderDataSize.Plugin,
            data,
            (image, _, i) => PlugIn.FromBinaryReader(image, i)
        );
    }

    internal DebugInfo ReadDebugInfo()
    {
        byte[] segment = DebugInfo.ReadSegment(
            _reader,
            _origin + Header.DebugStart,
            (int)Header.DebugSize
        );
        return DebugInfo.Read(segment, ReadScriptString, ReadLiteralBytes);
    }

    internal string ReadStringLiteral(int offset) =>
        ScriptStrings.Escape(ShiftJisHelper.Encoding.GetString(ReadLiteralBytes(offset)));

    internal byte[] ReadScriptString(int offset) => _strings.Add(ReadLiteralBytes(offset));

    internal double ReadDoubleLiteral(int offset)
    {
        long nowPosition = _reader.BaseStream.Position;
        _reader.BaseStream.Seek(_origin + Header.LiteralStart + offset, SeekOrigin.Begin);
        double ret = _reader.ReadDouble();
        _reader.BaseStream.Seek(nowPosition, SeekOrigin.Begin);
        return ret;
    }

    // A COM interface ID in its registry form: the first three fields are little-endian.
    //
    // COM のインターフェース ID をレジストリの形で返す。先頭の 3 つの欄はリトルエンディアン。
    internal string ReadIidCodeLiteral(int offset)
    {
        long nowPosition = _reader.BaseStream.Position;
        _reader.BaseStream.Seek(_origin + Header.LiteralStart + offset, SeekOrigin.Begin);
        byte[] buf = _reader.ReadBytes(0x10);
        _reader.BaseStream.Seek(nowPosition, SeekOrigin.Begin);
        var text = new StringBuilder();
        text.Append('{');
        AppendHex(text, buf, 0x03, 0x02, 0x01, 0x00);
        text.Append('-');
        AppendHex(text, buf, 0x05, 0x04);
        text.Append('-');
        AppendHex(text, buf, 0x07, 0x06);
        text.Append('-');
        AppendHex(text, buf, 0x08, 0x09);
        text.Append('-');
        AppendHex(text, buf, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F);
        text.Append('}');
        return text.ToString();
    }

    private static void AppendHex(StringBuilder text, byte[] buf, params int[] indexes)
    {
        foreach (int index in indexes)
        {
            text.Append(buf[index].ToString("X02", CultureInfo.InvariantCulture));
        }
    }

    private AxHeader ReadHeader()
    {
        if (_reader.BaseStream.Length - _origin < AxHeader.HeaderSize)
        {
            throw new HspDecoderException("AxData", Strings.FileHeaderMissing);
        }

        try
        {
            return AxHeader.FromBinaryReader(_reader);
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
        AxData data,
        Func<Ax3ImageReader, AxData, int, T> read
    )
    {
        for (int i = 0; i < count; i++)
        {
            _reader.BaseStream.Seek(_origin + start + ((int)size * i), SeekOrigin.Begin);
            table.Add(read(this, data, i));
        }
    }

    private string ReadString(int offset, int maxCount) =>
        ScriptStrings.Escape(ShiftJisHelper.Encoding.GetString(ReadBytes(offset, maxCount)));

    private byte[] ReadLiteralBytes(int offset)
    {
        byte[] bytes = ReadBytes(
            (int)(Header.LiteralStart + offset),
            (int)(Header.LiteralSize - offset)
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

    // Reads the NUL-terminated bytes at the offset, at most maxCount of them, so a missing
    // terminator near the end of the file stops at the segment instead of throwing.
    //
    // 指定位置から NUL 終端のバイト列を最大 maxCount バイト読む。ファイル末尾付近で終端が
    // 無くても、例外を投げずにセグメントの終わりで止まる。
    private byte[] ReadBytes(int offset, int maxCount)
    {
        Stream stream = _reader.BaseStream;
        long nowPosition = stream.Position;
        stream.Seek(_origin + offset, SeekOrigin.Begin);
        var bytes = new List<byte>();
        int next;
        while (bytes.Count < maxCount && (next = stream.ReadByte()) > 0)
        {
            bytes.Add((byte)next);
        }

        stream.Seek(nowPosition, SeekOrigin.Begin);
        return [.. bytes];
    }
}
