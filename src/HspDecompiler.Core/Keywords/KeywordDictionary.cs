using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using HspDecompiler.Core.Ax3;
using HspDecompiler.Core.Releases;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Keywords;

// The keyword table of every hspcmp release: the names of the codes a token word carries, the
// hspdef.as macros, and the parameter types. Each entry names the releases that have it, so one
// code can carry a different name in different releases, such as 0x0F 0x1E, resume up to 3.6
// and varprop from 3.7. A caller asks with a ReleaseProfile and gets the name its candidates use.
//
// 全 hspcmp リリースのキーワード表。トークンの語が持つコードの名前、hspdef.as のマクロ、引数の
// 型。項目はそれを持つリリースを示すため、1 つのコードがリリースごとに別の名前を持てる。
// 0x0F 0x1E は 3.6 までは resume、3.7 からは varprop である。呼び出し側は ReleaseProfile で
// 尋ね、その候補が使う名前を得る。
internal sealed class KeywordDictionary
{
    private const int ExtendedCommandType = 0x11;
    private const int ExtendedCommandValueThreshold = 0x1000;
    private const int PluginFunctionTypeBase = 0x12;

    private const string ResourcePrefix = "HspDecompiler.Core.Keywords.";

    private static readonly XNamespace s_ns = "urn:dehsp:dictionary";

    private static readonly Lazy<XmlSchemaSet> s_schemas = new(LoadSchemas);

    private static readonly Lazy<KeywordDictionary> s_builtIn = new(LoadBuiltIn);

    private static readonly CompositeFormat s_duplicateEntryFormat = CompositeFormat.Parse(
        Strings.DictionaryDuplicateEntry
    );

    private static readonly CompositeFormat s_variantMismatchFormat = CompositeFormat.Parse(
        Strings.DictionaryVariantMismatch
    );

    private static readonly CompositeFormat s_unknownReleaseFormat = CompositeFormat.Parse(
        Strings.DictionaryUnknownRelease
    );

    private readonly Dictionary<HspDictionaryKey, List<Keyword>> _codes = [];
    private readonly Dictionary<(string Function, int Argument), List<Macro>> _macros = [];
    private readonly Dictionary<int, string> _paramDictionary = [];

    private KeywordDictionary() { }

    private readonly record struct Keyword(HspDictionaryValue Value, ReleaseSpan Span);

    private readonly record struct Macro(string Name, ReleaseSpan Span);

    internal static KeywordDictionary BuiltIn => s_builtIn.Value;

    internal string Version { get; private set; } = "";

    // Throws IOException, UnauthorizedAccessException, XmlException, XmlSchemaException or
    // InvalidDataException, each naming what is wrong and, for XML errors, the line.
    //
    // IOException・UnauthorizedAccessException・XmlException・XmlSchemaException・
    // InvalidDataException を投げる。どれも原因を示し、XML の誤りなら行番号も含む。
    internal static KeywordDictionary FromFile(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        return FromStream(stream);
    }

    // The table ships inside the assembly so the decompiler runs without a file beside it.
    //
    // 表はアセンブリに埋め込まれているため、隣にファイルが無くても動作する。
    private static KeywordDictionary LoadBuiltIn()
    {
        using Stream stream = OpenResource("Dictionary.xml");
        return FromStream(stream);
    }

    private static Stream OpenResource(string name) =>
        typeof(KeywordDictionary).Assembly.GetManifestResourceStream(ResourcePrefix + name)
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

    // A section's since and until are the defaults of the entries inside it, and an entry's own
    // attributes override them.
    //
    // 節の since と until は中の項目の既定値で、項目自身の属性がそれを上書きする。
    private static KeywordDictionary FromStream(Stream stream)
    {
        XElement root = LoadValidated(stream).Root!;
        var dictionary = new KeywordDictionary { Version = (string)root.Attribute("version")! };
        foreach (XElement codes in root.Elements(s_ns + "codes"))
        {
            ReleaseSpan outer = ReadSpan(codes, ReleaseSpan.All);
            foreach (XElement code in codes.Elements(s_ns + "code"))
            {
                dictionary.AddCode(code, ReadSpan(code, outer));
            }
        }

        foreach (XElement macros in root.Elements(s_ns + "macros"))
        {
            ReleaseSpan outer = ReadSpan(macros, ReleaseSpan.All);
            foreach (XElement macro in macros.Elements(s_ns + "macro"))
            {
                dictionary.AddMacro(macro, ReadSpan(macro, outer));
            }
        }

        foreach (XElement param in root.Element(s_ns + "paramTypes")!.Elements(s_ns + "paramType"))
        {
            dictionary.AddParam(param);
        }

        return dictionary;
    }

    // Variants of one code share kind, flags and priority, because the lexer parses a code
    // before the release is known; their spans must not share a release.
    //
    // 1 つのコードの変種は kind・flags・priority を共有する。字句解析はリリースが分かる前に
    // コードを解析するため。変種の範囲はリリースを共有してはならない。
    private void AddCode(XElement code, ReleaseSpan span)
    {
        HspDictionaryKey key = ReadKey(code);
        HspDictionaryValue value = ReadValue(code);
        if (!_codes.TryGetValue(key, out List<Keyword>? variants))
        {
            _codes[key] = [new Keyword(value, span)];
            return;
        }

        foreach (Keyword variant in variants)
        {
            if (variant.Span.Overlaps(span))
            {
                throw Invalid(code, s_duplicateEntryFormat, key.ToString());
            }

            if (
                variant.Value._type != value._type
                || variant.Value._extra != value._extra
                || variant.Value._operatorPriority != value._operatorPriority
            )
            {
                throw Invalid(code, s_variantMismatchFormat, key.ToString());
            }
        }

        variants.Add(new Keyword(value, span));
    }

    private void AddMacro(XElement macro, ReleaseSpan span)
    {
        string name = (string)macro.Attribute("name")!;
        var key = ((string)macro.Attribute("function")!, (int)macro.Attribute("argument")!);
        if (!_macros.TryGetValue(key, out List<Macro>? variants))
        {
            variants = [];
            _macros[key] = variants;
        }

        if (variants.Exists(m => m.Span.Overlaps(span)))
        {
            throw Invalid(macro, s_duplicateEntryFormat, name);
        }

        variants.Add(new Macro(name, span));
    }

    private void AddParam(XElement param)
    {
        if (
            !_paramDictionary.TryAdd(
                ReadHex(param, "code")!.Value,
                (string)param.Attribute("name")!
            )
        )
        {
            throw Invalid(param, s_duplicateEntryFormat, (string)param.Attribute("code")!);
        }
    }

    private static ReleaseSpan ReadSpan(XElement element, ReleaseSpan outer)
    {
        string? since = (string?)element.Attribute("since");
        string? until = (string?)element.Attribute("until");
        return ReleaseSpan.Parse(since, until, outer)
            ?? throw Invalid(element, s_unknownReleaseFormat, since + "-" + until);
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

    private static InvalidDataException Invalid(
        XElement element,
        CompositeFormat format,
        string key
    ) =>
        new(
            string.Format(
                CultureInfo.InvariantCulture,
                format,
                key,
                ((IXmlLineInfo)element).LineNumber
            )
        );

    // The entry a code has in the releases of the scope: the variant of the newest candidate
    // that has the code, or, when no candidate has it, the newest variant of the same major
    // version, so a code a later release added still reads. COM and plug-in codes, which carry
    // an index rather than a keyword, take fixed entries.
    //
    // 範囲内のリリースでコードが持つ項目。コードを持つ最も新しい候補の変種を取り、どの候補も
    // 持たなければ同じメジャーバージョンの最も新しい変種を取る。後のリリースが加えたコードも
    // 読めるようにするため。キーワードではなく番号を持つ COM とプラグインのコードは固定の項目を
    // 取る。
    internal bool CodeLookUp(
        HspDictionaryKey key,
        ReleaseProfile scope,
        out HspDictionaryValue value
    )
    {
        if (Pick(Variants(key), scope) is { } keyword)
        {
            value = keyword.Value;
            return true;
        }

        value = default;
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

    // The name a code has for the release profile, or null when no entry of its major version
    // has it.
    //
    // リリースのプロファイルでのコードの名前。そのメジャーバージョンのどの項目にも無ければ null。
    internal string? NameOf(int type, int value, ReleaseProfile release) =>
        Pick(Variants(new HspDictionaryKey(type, value)), release)?.Value._name;

    // The name of an entry every candidate has, with no fallback to another release of the
    // major version. HSP 1 asks this of a byte that is a command in 1.2f and an operator in 1.1.
    //
    // すべての候補が持つ項目の名前。同じメジャーバージョンの別のリリースには頼らない。HSP 1 は
    // 1.2f では命令、1.1 では演算子であるバイトについてこれを尋ねる。
    internal string? ExactNameOf(int type, int value, ReleaseProfile release) =>
        Variants(new HspDictionaryKey(type, value))
            .Where(k => !release.Candidates.IsEmpty && release.Candidates.All(k.Span.Contains))
            .Select(k => k.Value._name)
            .FirstOrDefault();

    // True when the code has entries that name it differently across releases, so its name is
    // settled only once the release is.
    //
    // リリースによって名前の違う項目をコードが持つ時に真。その名前はリリースが決まって初めて
    // 決まる。
    internal bool HasVariants(int type, int value) =>
        Variants(new HspDictionaryKey(type, value)).Count > 1;

    // The releases that have the code, from the first variant's to the last's; null for a code
    // without an entry.
    //
    // コードを持つリリースの範囲。最初の変種から最後の変種まで。項目の無いコードは null。
    internal ReleaseSpan? SpanOf(int type, int value)
    {
        List<Keyword> variants = Variants(new HspDictionaryKey(type, value));
        return variants.Count == 0
            ? null
            : variants.Select(k => k.Span).Aggregate((all, span) => all.Hull(span));
    }

    // True when a code some candidate has carries this name.
    //
    // いずれかの候補が持つコードがこの名前を持つ時に真。
    internal bool IsName(string name, ReleaseProfile scope) =>
        KeywordsIn(scope).Any(k => k.Value._name == name);

    // The hspdef.as macro that spells function(argument), when every candidate has it.
    //
    // function(argument) を表す hspdef.as のマクロ。すべての候補が持つ時だけ返す。
    internal string? MacroFor(string function, int argument, ReleaseProfile release) =>
        _macros.TryGetValue((function, argument), out List<Macro>? variants)
            ? variants
                .Where(m => release.Candidates.All(m.Span.Contains))
                .Select(m => m.Name)
                .FirstOrDefault()
            : null;

    internal bool ParamLookUp(int paramKey, out string? paramTypeName) =>
        _paramDictionary.TryGetValue(paramKey, out paramTypeName);

    // Every keyword some candidate has and every parameter type, lowered, which a recovered
    // name must not shadow.
    //
    // 復元した名前が隠してはならない、いずれかの候補が持つ全キーワードと引数型を小文字で返す。
    internal HashSet<string> GetAllNames(ReleaseProfile scope)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Keyword keyword in KeywordsIn(scope))
        {
            names.Add(keyword.Value._name.ToLowerInvariant());
        }

        names.UnionWith(_paramDictionary.Values.Select(name => name.ToLowerInvariant()));
        return names;
    }

    internal List<string> GetAllFuncName(ReleaseProfile scope)
    {
        var functionNames = new List<string>();
        foreach (Keyword keyword in KeywordsIn(scope))
        {
            if (
                keyword.Value._type
                is HspCodeType.HspFunction
                    or HspCodeType.IfStatement
                    or HspCodeType.OnEventStatement
                    or HspCodeType.OnStatement
                    or HspCodeType.McallStatement
            )
            {
                functionNames.Add(keyword.Value._name.ToLowerInvariant());
            }
        }

        functionNames.AddRange(_paramDictionary.Values.Select(name => name.ToLowerInvariant()));
        return functionNames;
    }

    private IEnumerable<Keyword> KeywordsIn(ReleaseProfile scope) =>
        _codes.Values.SelectMany(variants => variants).Where(k => k.Span.Overlaps(scope));

    // The entries of the exact code, else those for every value of its type.
    //
    // そのコードの項目。無ければその型のすべての値に一致する項目。
    private List<Keyword> Variants(HspDictionaryKey key) =>
        _codes.TryGetValue(key, out List<Keyword>? exact) ? exact
        : _codes.TryGetValue(new HspDictionaryKey(key._type, null), out List<Keyword>? all) ? all
        : [];

    private static Keyword? Pick(List<Keyword> variants, ReleaseProfile scope)
    {
        for (int i = scope.Candidates.Length - 1; i >= 0; i--)
        {
            CompilerRelease candidate = scope.Candidates[i];
            if (variants.Exists(k => k.Span.Contains(candidate)))
            {
                return variants.Find(k => k.Span.Contains(candidate));
            }
        }

        if (scope.Candidates.IsEmpty)
        {
            return null;
        }

        var major = ReleaseSpan.OfMajor(scope.Candidates[^1].Major);
        return variants
            .Where(k => k.Span.Overlaps(major))
            .OrderByDescending(k => k.Span.Last)
            .Cast<Keyword?>()
            .FirstOrDefault();
    }
}
