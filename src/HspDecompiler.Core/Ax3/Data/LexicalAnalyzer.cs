using System;
using System.IO;
using HspDecompiler.Core.Ax3.Data.Analyzer;
using HspDecompiler.Core.Ax3.Data.Primitive;
using HspDecompiler.Core.Ax3.Dictionary;

namespace HspDecompiler.Core.Ax3.Data;

internal class LexicalAnalyzer
{
    internal LexicalAnalyzer(Hsp3Dictionary theDic)
    {
        ArgumentNullException.ThrowIfNull(theDic);

        _dictionary = theDic;
    }

    private readonly Hsp3Dictionary? _dictionary;
    private int _tokenOffset;

    internal TokenCollection Analyze(AxData data)
    {
        if (!data.IsStarted)
        {
            throw new InvalidOperationException();
        }

        var stream = new TokenCollection();
        BinaryReader reader = data.Reader!;
        long sizeOfCode = data.Header!.CodeSize;
        long startOfCode = data.StartOfCode;
        _tokenOffset = 0;
        reader.BaseStream.Seek(startOfCode, SeekOrigin.Begin);
        while (_tokenOffset < sizeOfCode)
        {
            PrimitiveToken code = ReadPrimitive(reader, data);
            stream.Add(code);
        }

        return stream;
    }

    private PrimitiveToken ReadPrimitive(BinaryReader reader, AxData data)
    {
        int theTokenOffset = _tokenOffset;
        int type = reader.ReadByte();
        int flag = reader.ReadByte();
        int value;
        _tokenOffset += 1;
        if ((flag & HspTokenFlag.LongTypeValue) == HspTokenFlag.LongTypeValue)
        {
            value = reader.ReadInt32();
            _tokenOffset += 2;
        }
        else
        {
            value = reader.ReadUInt16();
            _tokenOffset += 1;
        }

        if ((flag & HspTokenFlag.SingleItem) != 0)
        {
            data.HasSingleItemFlags = true;
        }

        var key = new HspDictionaryKey { _type = type, _value = value };
        bool known = _dictionary!.CodeLookUp(key, out HspDictionaryValue dicValue);
        int extraValue = -1;
        if (
            known
            && dicValue._extra.HasFlag(HspCodeExtraOptions.HasExtraInt16)
            && ((flag & HspTokenFlag.LineHead) == HspTokenFlag.LineHead)
        )
        {
            extraValue = reader.ReadUInt16();
            _tokenOffset += 1;
        }

        PrimitiveToken ret = CreatePrimitive(
            data,
            known ? dicValue : default,
            theTokenOffset,
            type,
            flag,
            value,
            extraValue
        );
        ret.SetName();

        return ret;
    }

    private static PrimitiveToken CreatePrimitive(
        AxData data,
        HspDictionaryValue dicValue,
        int theTokenOffset,
        int type,
        int flag,
        int value,
        int extraValue
    )
    {
        var dataset = new PrimitiveTokenDataSet
        {
            _parent = data,
            _dicValue = dicValue,
            _tokenOffset = theTokenOffset,
            _type = type,
            _flag = flag,
            _value = value,
            _name = dicValue._name,
        };
        if (dicValue._type is HspCodeType.DoubleValue or HspCodeType.StringValue)
        {
            data.LiteralAnchors.Add((value, theTokenOffset));
        }

        return dicValue._type switch
        {
            HspCodeType.Label => new LabelPrimitive(dataset),
            HspCodeType.IntegerValue => new IntegerPrimitive(dataset),
            HspCodeType.DoubleValue => new DoublePrimitive(dataset, data.ReadDoubleLiteral(value)),
            HspCodeType.StringValue => new StringPrimitive(dataset, data.ReadStringLiteral(value)),
            HspCodeType.Symbol => new SymbolPrimitive(dataset),
            HspCodeType.Param => new ParameterPrimitive(dataset),
            HspCodeType.Variable => new GlobalVariablePrimitive(dataset),
            HspCodeType.Operator => new OperatorPrimitive(dataset),
            HspCodeType.IfStatement or HspCodeType.ElseStatement when extraValue >= 0 =>
                new IfStatementPrimitive(dataset, extraValue),
            HspCodeType.IfStatement or HspCodeType.ElseStatement or HspCodeType.HspFunction =>
                new HspFunctionPrimitive(dataset),
            HspCodeType.OnStatement => new OnFunctionPrimitive(dataset),
            HspCodeType.OnEventStatement => new OnEventFunctionPrimitive(dataset),
            HspCodeType.McallStatement => new McallFunctionPrimitive(dataset),
            HspCodeType.UserFunction => new UserFunctionPrimitive(dataset),
            HspCodeType.DllFunction => new DllFunctionPrimitive(dataset),
            HspCodeType.PlugInFunction => new PlugInFunctionPrimitive(dataset),
            HspCodeType.ComFunction => new ComFunctionPrimitive(dataset),
            HspCodeType.None or _ => new UnknownPrimitive(dataset),
        };
    }
}
