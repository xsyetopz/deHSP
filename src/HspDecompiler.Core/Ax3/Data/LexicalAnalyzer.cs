using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using HspDecompiler.Core.Ax3.Data.Analyzer;
using HspDecompiler.Core.Ax3.Data.Primitive;
using HspDecompiler.Core.Ax3.Reading;
using HspDecompiler.Core.Keywords;

namespace HspDecompiler.Core.Ax3.Data;

internal class LexicalAnalyzer
{
    internal LexicalAnalyzer(KeywordDictionary theDic)
    {
        ArgumentNullException.ThrowIfNull(theDic);

        _dictionary = theDic;
    }

    // TYPE_PROGCMD and the await code within it (hsp3struct.h, Dictionary.xml).
    //
    // TYPE_PROGCMD とその中の await のコード(hsp3struct.h、Dictionary.xml)。
    private const int ProgramCommandType = 0x0F;
    private const int AwaitCode = 0x08;

    private readonly KeywordDictionary? _dictionary;
    private int _tokenOffset;

    internal TokenCollection Analyze(Ax3ImageReader image, AxData data)
    {
        var stream = new TokenCollection();
        BinaryReader reader = image.Reader;
        long sizeOfCode = image.Header.CodeSize;
        _tokenOffset = 0;
        reader.BaseStream.Seek(image.StartOfCode, SeekOrigin.Begin);
        while (_tokenOffset < sizeOfCode)
        {
            PrimitiveToken code = ReadPrimitive(image, data);
            stream.Add(code);
        }

        return stream;
    }

    private PrimitiveToken ReadPrimitive(Ax3ImageReader image, AxData data)
    {
        BinaryReader reader = image.Reader;
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

        if (type == ProgramCommandType && value == AwaitCode)
        {
            data.UsesAwait = true;
        }

        var key = new HspDictionaryKey { _type = type, _value = value };
        bool known = _dictionary!.CodeLookUp(key, data.Release, out HspDictionaryValue dicValue);
        NoteKeyword(data, type, value);
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

        HspDictionaryValue found = known ? dicValue : default;
        var dataset = new PrimitiveTokenDataSet
        {
            _parent = data,
            _dicValue = found,
            _tokenOffset = theTokenOffset,
            _type = type,
            _flag = flag,
            _value = value,
            _name = found._name,
        };
        PrimitiveToken ret = CreatePrimitive(image, data, dataset, extraValue);
        ret.SetName();

        return ret;
    }

    // Narrows the releases whose keywords the code uses: a code hspcmp writes only from 3.5 on
    // rules out every earlier release. A code no release of the format word has, such as a
    // plug-in code that shares a type with an HSP 2 keyword, says nothing.
    //
    // コードが使うキーワードを持つリリースに範囲を絞る。3.5 から hspcmp が書くコードはそれより
    // 前のリリースをすべて除く。形式のワードのどのリリースも持たないコード、例えば HSP 2 の
    // キーワードと型を共有するプラグインのコードは何も示さない。
    private void NoteKeyword(AxData data, int type, int value)
    {
        if (_dictionary!.SpanOf(type, value) is { } span && span.Overlaps(data.Release))
        {
            data.KeywordSpan = data.KeywordSpan.Intersect(span);
        }
    }

    // IDE0072 asks for every HspCodeType member, so None stays named next to the discard even
    // though ReSharper sees the pattern as redundant.
    //
    // IDE0072 は HspCodeType の全メンバーを求めるため、ReSharper が冗長と見なしても None を
    // 破棄パターンの隣に明示する。
    [SuppressMessage("ReSharper", "PatternIsRedundant", Justification = "IDE0072 wants None")]
    private static PrimitiveToken CreatePrimitive(
        Ax3ImageReader image,
        AxData data,
        PrimitiveTokenDataSet dataset,
        int extraValue
    )
    {
        HspCodeType kind = dataset._dicValue._type;
        int value = dataset._value;
        if (kind is HspCodeType.DoubleValue or HspCodeType.StringValue)
        {
            data.LiteralAnchors.Add((value, dataset._tokenOffset));
        }

        return kind switch
        {
            HspCodeType.Label => new LabelPrimitive(dataset),
            HspCodeType.IntegerValue => new IntegerPrimitive(dataset),
            HspCodeType.DoubleValue => new DoublePrimitive(dataset, image.ReadDoubleLiteral(value)),
            HspCodeType.StringValue => new StringPrimitive(dataset, image.ReadScriptString(value)),
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
