using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HspDecompiler.Core.Ax3.Dictionary;

namespace HspDecompiler.Core.Ax3;

internal class Hsp3Dictionary
{
    private const int ExtendedCommandType = 0x11;
    private const int ExtendedCommandValueThreshold = 0x1000;
    private const int PluginFunctionTypeBase = 0x12;

    private Hsp3Dictionary() { }

    private readonly Dictionary<HspDictionaryKey, HspDictionaryValue> _codeDictionary = [];
    private readonly Dictionary<int, string> _paramDictionary = [];

    private const string EmbeddedResourceName = "HspDecompiler.Core.Ax3.Dictionary.json";

    private static readonly Lazy<Hsp3Dictionary> s_builtIn = new(LoadBuiltIn);

    internal static Hsp3Dictionary BuiltIn => s_builtIn.Value;

    internal string Version { get; private set; } = "";

    internal static Hsp3Dictionary? FromFile(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return FromStream(stream);
        }
        catch (Exception ex)
            when (ex
                    is IOException
                        or UnauthorizedAccessException
                        or JsonException
                        or ArgumentException
            )
        {
            return null;
        }
    }

    // The table ships inside the assembly so the decompiler runs without a file beside it.

    // 表はアセンブリに埋め込まれているため、隣にファイルが無くても動作する。
    private static Hsp3Dictionary LoadBuiltIn()
    {
        using Stream stream = typeof(Hsp3Dictionary).Assembly.GetManifestResourceStream(
            EmbeddedResourceName
        ) ?? throw new InvalidOperationException(EmbeddedResourceName);
        return FromStream(stream);
    }

    private static Hsp3Dictionary FromStream(Stream stream)
    {
        DictionaryDocument document = JsonSerializer.Deserialize(
            stream,
            DictionaryJsonContext.Default.DictionaryDocument
        ) ?? throw new JsonException(EmbeddedResourceName);
        var dictionary = new Hsp3Dictionary { Version = document.Version };
        foreach (
            HspDictionaryKey key in from entry in document.Codes
            let key = new HspDictionaryKey(entry.Type, entry.Value)
            let value = new HspDictionaryValue(
                entry.Name,
                entry.Kind,
                entry.Flags,
                entry.Priority ?? -1
            )
            where !dictionary._codeDictionary.TryAdd(key, value)
            select key
        )
        {
            throw new JsonException(key.ToString());
        }

        foreach (
            ParamTypeEntry entry in document.ParamTypes.Where(entry =>
                !dictionary._paramDictionary.TryAdd(entry.Code, entry.Name)
            )
        )
        {
            throw new JsonException(entry.Name);
        }

        return dictionary;
    }

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
