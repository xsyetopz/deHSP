using System;
using System.Collections.Generic;
using System.Linq;
using HspDecompiler.Core.Ax3.Data.PP;

namespace HspDecompiler.Core.Ax3.Data;

// Gives parameters and labels the names a debug build recorded (codegen.cpp PutDIParams and
// PutDILabels). A name declared inside a module carries "@module"; the suffix goes, and the
// output spells it again where the line needs it (ModuleScope). HSP names ignore case, and a
// name that would then clash with a variable, a function, a keyword or another recovered name
// keeps the generated one, because the recompile would bind it to the wrong symbol.
//
// デバッグビルドが記録した名前(codegen.cpp の PutDIParams と PutDILabels)を引数とラベルに
// 付ける。モジュール内で宣言した名前には "@モジュール" が付く。接尾辞は外し、出力が必要な行で
// 付け直す(ModuleScope)。HSP の名前は大文字と小文字を区別せず、外した結果が変数・関数・
// キーワード・他の復元名と衝突する名前は、再コンパイルで別のシンボルに結び付くため生成名の
// ままにする。
internal static class DebugNames
{
    internal static void ApplyParameterNames(AxData data)
    {
        Dictionary<int, byte[]> recorded = data.DebugInfo.ParameterNames;
        if (recorded.Count == 0)
        {
            return;
        }

        HashSet<string> taken = TakenNames(data);
        foreach (Function function in data.Functions)
        {
            var local = new HashSet<string>(StringComparer.Ordinal);
            foreach (Param param in function.Parameters)
            {
                string? name = Recover(data, recorded, param.Index);
                if (
                    name != null
                    && IsIdentifier(name)
                    && !taken.Contains(Key(name))
                    && local.Add(Key(name))
                )
                {
                    param.DebugName = name;
                }
            }
        }
    }

    // Returns the recovered label names by object-table index, for labels whose name is unique.
    //
    // 名前が一意なラベルについて、オブジェクトテーブル番号ごとの復元名を返す。
    internal static Dictionary<int, string> LabelNames(AxData data, IReadOnlyList<Label> labels)
    {
        var names = new Dictionary<int, string>();
        var keys = new Dictionary<int, string>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        HashSet<string> modules = [.. labels.Select(l => ExitModule(data, l)).OfType<string>()];
        foreach (Label label in labels)
        {
            string? name = Recover(data, data.DebugInfo.LabelNames, label.Index);
            if (name == null || !IsIdentifier(name))
            {
                continue;
            }

            string? module = RecordedModule(data, label);
            string key = Key(
                module != null && modules.Contains(module) ? name + "@" + module : name
            );
            seen[key] = seen.TryGetValue(key, out int count) ? count + 1 : 1;
            keys[label.Index] = key;
            names[label.Index] = "*" + name;
        }

        return names
            .Where(entry => seen[keys[entry.Key]] == 1)
            .ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    // The module of a "*_name_exit" label, which hspcmp writes for each #global (token.cpp
    // PP_Global); a rebuilt module keeps its labels apart from those of other modules.
    //
    // "*_名前_exit" ラベルのモジュール。hspcmp は #global ごとにこのラベルを書く(token.cpp の
    // PP_Global)。再構築したモジュールのラベルは他のモジュールのラベルと区別される。
    internal static string? ExitModule(AxData data, Label label)
    {
        const string prefix = "_";
        const string suffix = "_exit";
        string? name = data.GetDebugName(data.DebugInfo.LabelNames, label.Index);
        return
            name != null
            && name.Length > prefix.Length + suffix.Length
            && name.StartsWith(prefix, StringComparison.Ordinal)
            && name.EndsWith(suffix, StringComparison.Ordinal)
            && !name.Contains('@', StringComparison.Ordinal)
            ? name[prefix.Length..^suffix.Length]
            : null;
    }

    // The module a recorded label name carries after '@', or null for a global label.
    //
    // 記録されたラベル名が '@' の後に持つモジュール。グローバルのラベルなら null。
    internal static string? RecordedModule(AxData data, Label label)
    {
        string? name = data.GetDebugName(data.DebugInfo.LabelNames, label.Index);
        int at = name?.IndexOf('@', StringComparison.Ordinal) ?? -1;
        return at >= 0 ? name![(at + 1)..] : null;
    }

    private static string? Recover(AxData data, Dictionary<int, byte[]> recorded, int index)
    {
        string? name = data.GetDebugName(recorded, index);
        if (name == null)
        {
            return null;
        }

        int at = name.IndexOf('@', StringComparison.Ordinal);
        return at >= 0 ? name[..at] : name;
    }

    private static HashSet<string> TakenNames(AxData data)
    {
        HashSet<string> taken = data.Dictionary!.GetAllNames();
        // A module variable is written bare inside its module, so its bare name is taken too.
        //
        // モジュール変数はそのモジュール内では裸で書くため、裸の名前も使用済みにする。
        for (int i = 0; data.GetVariableName(i) is { } variable; i++)
        {
            taken.Add(Key(variable));
            int at = variable.IndexOf('@', StringComparison.Ordinal);
            taken.Add(Key(at >= 0 ? variable[..at] : variable));
        }

        foreach (Function function in data.Functions)
        {
            if (function.FunctionName is { } name)
            {
                taken.Add(Key(name));
            }
        }

        return taken;
    }

    private static string Key(string name) => name.ToLowerInvariant();

    // hspcmp reads every byte past ASCII as part of a name (token.cpp GetToken), so Japanese
    // names stay; on the ASCII side only letters, digits and '_' are kept, which it also reads.
    //
    // hspcmp は ASCII 外のバイトを名前の一部として読む(token.cpp の GetToken)ため、日本語の
    // 名前も残せる。ASCII では同じく名前として読む英字・数字・'_' だけを残す。
    private static bool IsIdentifier(string name) =>
        name.Length > 0
        && !char.IsAsciiDigit(name[0])
        && name.All(c => c > 0x7F || char.IsAsciiLetterOrDigit(c) || c == '_');
}
