using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HspDecompiler.Core.Ax3.Dictionary;

internal sealed class DictionaryDocument
{
    public string Version { get; init; } = "";
    public List<CodeEntry> Codes { get; init; } = [];
    public List<ParamTypeEntry> ParamTypes { get; init; } = [];
}

// A missing Value matches every value code of the type, as hspcmp stores variables, literals
// and labels as one type code with an index or offset in the value field.
// Value が無い項目は型のすべての値コードに一致する。hspcmp は変数・リテラル・ラベルを
// 1 つの型コードで表し、値フィールドに番号やオフセットを入れるため。
internal sealed class CodeEntry
{
    public int Type { get; init; }
    public int? Value { get; init; }
    public string Name { get; init; } = "";
    public HspCodeType Kind { get; init; }
    public int? Priority { get; init; }
    public HspCodeExtraOptions Flags { get; init; }
}

internal sealed class ParamTypeEntry
{
    public int Code { get; init; }
    public string Name { get; init; } = "";
    public int Size { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UseStringEnumConverter = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
)]
[JsonSerializable(typeof(DictionaryDocument))]
internal sealed partial class DictionaryJsonContext : JsonSerializerContext;
