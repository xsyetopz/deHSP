using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using HspDecompiler.Core.Ax3.Dictionary;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax3;

internal class Hsp3Dictionary
{
    private const int ExtendedCommandType = 0x11;
    private const int ExtendedCommandValueThreshold = 0x1000;
    private const int PluginFunctionTypeBase = 0x12;

    private Hsp3Dictionary() { }

    private readonly Dictionary<HspDictionaryKey, HspDictionaryValue> _codeDictionary = [];
    private readonly Dictionary<int, string> _paramDictionary = [];

    private const string ResourcePrefix = "HspDecompiler.Core.Ax3.";

    private static readonly XNamespace s_ns = "urn:dehsp:dictionary";

    private static readonly Lazy<XmlSchemaSet> s_schemas = new(LoadSchemas);

    private static readonly Lazy<Hsp3Dictionary> s_builtIn = new(LoadBuiltIn);

    internal static Hsp3Dictionary BuiltIn => s_builtIn.Value;

    internal string Version { get; private set; } = "";

    // Throws IOException, UnauthorizedAccessException, XmlException, XmlSchemaException or
    // InvalidDataException, each naming what is wrong and, for XML errors, the line.
    //
    // IOException・UnauthorizedAccessException・XmlException・XmlSchemaException・
    // InvalidDataException を投げる。どれも原因を示し、XML の誤りなら行番号も含む。
    internal static Hsp3Dictionary FromFile(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return FromStream(stream);
    }

    // The table ships inside the assembly so the decompiler runs without a file beside it.
    //
    // 表はアセンブリに埋め込まれているため、隣にファイルが無くても動作する。
    private static Hsp3Dictionary LoadBuiltIn()
    {
        using Stream stream = OpenResource("Dictionary.xml");
        return FromStream(stream);
    }

    private static Stream OpenResource(string name) =>
        typeof(Hsp3Dictionary).Assembly.GetManifestResourceStream(ResourcePrefix + name)
        ?? throw new InvalidOperationException(ResourcePrefix + name);

    private static XmlSchemaSet LoadSchemas()
    {
        using Stream stream = OpenResource("Dictionary.xsd");
        using var reader = XmlReader.Create(stream, SafeSettings());
        var schemas = new XmlSchemaSet { XmlResolver = null };
        schemas.Add(null, reader);
        schemas.Compile();
        return schemas;
    }

    // DTDs and external resolution stay off because a custom dictionary is user input.
    //
    // 独自の辞書は利用者の入力なので、DTD と外部参照の解決は無効にしておく。
    private static XmlReaderSettings SafeSettings() =>
        new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

    // Warnings count as errors: a root element outside the dictionary namespace raises only a
    // "no schema information" warning and would otherwise pass unchecked.
    //
    // 警告もエラーとして扱う。辞書の名前空間に無いルート要素は「スキーマ情報が無い」という
    // 警告しか出さず、そのままでは検証されずに通ってしまうため。
    private static XDocument LoadValidated(Stream stream)
    {
        XmlReaderSettings settings = SafeSettings();
        settings.ValidationType = ValidationType.Schema;
        settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        settings.Schemas = s_schemas.Value;
        settings.ValidationEventHandler += (_, e) => throw e.Exception;
        using var reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader, LoadOptions.SetLineInfo);
    }

    private static Hsp3Dictionary FromStream(Stream stream)
    {
        XElement root = LoadValidated(stream).Root!;
        var dictionary = new Hsp3Dictionary { Version = (string)root.Attribute("version")! };
        foreach (XElement code in root.Element(s_ns + "codes")!.Elements(s_ns + "code"))
        {
            HspDictionaryKey key = ReadKey(code);
            if (!dictionary._codeDictionary.TryAdd(key, ReadValue(code)))
            {
                throw Duplicate(code, key.ToString());
            }
        }

        foreach (XElement param in root.Element(s_ns + "paramTypes")!.Elements(s_ns + "paramType"))
        {
            if (
                !dictionary._paramDictionary.TryAdd(
                    ReadHex(param, "code")!.Value,
                    (string)param.Attribute("name")!
                )
            )
            {
                throw Duplicate(param, (string)param.Attribute("code")!);
            }
        }

        return dictionary;
    }

    private static HspDictionaryKey ReadKey(XElement code) =>
        new(ReadHex(code, "type")!.Value, ReadHex(code, "value"));

    private static HspDictionaryValue ReadValue(XElement code) =>
        new(
            (string?)code.Attribute("name") ?? "",
            Enum.Parse<HspCodeType>((string)code.Attribute("kind")!),
            ReadFlags((string?)code.Attribute("flags")),
            (int?)code.Attribute("priority") ?? -1
        );

    private static int? ReadHex(XElement element, string name) =>
        element.Attribute(name) is { } attribute
            ? int.Parse(
                attribute.Value.AsSpan(2),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture
            )
            : null;

    private static HspCodeExtraOptions ReadFlags(string? flags) =>
        (flags ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Aggregate(HspCodeExtraOptions.None, (all, flag) =>
                all | Enum.Parse<HspCodeExtraOptions>(flag)
            );

    // The schema's xs:unique rules catch most duplicates; this check also catches two entries
    // without a value for the same type, which xs:unique skips because a field is missing.
    //
    // 重複の大半はスキーマの xs:unique が検出する。ここでは同じ型で value の無い項目が 2 つある
    // 場合も検出する。フィールドが欠けた項目を xs:unique は対象外にするため。
    private static InvalidDataException Duplicate(XElement element, string key) =>
        new(
            string.Format(
                CultureInfo.InvariantCulture,
                s_duplicateEntryFormat,
                key,
                ((IXmlLineInfo)element).LineNumber
            )
        );

    private static readonly CompositeFormat s_duplicateEntryFormat = CompositeFormat.Parse(
        Strings.DictionaryDuplicateEntry
    );

    internal bool CodeLookUp(HspDictionaryKey key, out HspDictionaryValue value)
    {
        if (_codeDictionary.TryGetValue(key, out value))
        {
            return true;
        }

        var newkey = new HspDictionaryKey(key) { _value = -1, _allValue = true };
        if (_codeDictionary.TryGetValue(newkey, out value))
        {
            return true;
        }

        if (key is { _type: ExtendedCommandType, _value: >= ExtendedCommandValueThreshold })
        {
            value._name = "comfunc";
            value._type = HspCodeType.ComFunction;
            value._extra = HspCodeExtraOptions.None;
            return true;
        }

        if (key._type < PluginFunctionTypeBase)
        {
            return false;
        }

        value._name = "pluginFunction";
        value._operatorPriority = key._type - PluginFunctionTypeBase;
        value._type = HspCodeType.PlugInFunction;
        value._extra = HspCodeExtraOptions.None;
        return true;
    }

    internal bool ParamLookUp(int paramKey, out string? paramTypeName) =>
        _paramDictionary.TryGetValue(paramKey, out paramTypeName);

    internal List<string> GetAllFuncName()
    {
        var functionNames = new List<string>();
        foreach (HspDictionaryValue value in _codeDictionary.Values)
        {
            if (
                value._type
                is HspCodeType.HspFunction
                    or HspCodeType.IfStatement
                    or HspCodeType.OnEventStatement
                    or HspCodeType.OnStatement
                    or HspCodeType.McallStatement
            )
            {
                functionNames.Add(value._name.ToLowerInvariant());
            }
        }

        functionNames.AddRange(_paramDictionary.Values.Select(name => name.ToLowerInvariant()));

        return functionNames;
    }
}
