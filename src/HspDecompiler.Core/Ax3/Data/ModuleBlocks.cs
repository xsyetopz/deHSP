using System;
using System.Collections.Generic;
using System.Linq;
using HspDecompiler.Core.Ax3.Data.Line;
using HspDecompiler.Core.Ax3.Data.PP;

namespace HspDecompiler.Core.Ax3.Data;

// Rebuilds the #module blocks of a debug build. hspcmp expands "#module name" to
// "goto *_name_exit" and "#global" to "*_name_exit" (token.cpp PP_Module and PP_Global), and a
// debug build records that label name, so each such goto and label become #module and #global
// again. The names inside then take their module scope back (ModuleScope), a module with
// members drops its #struct line, and the module hspcmp named "m0", "m1" and so on for a
// "#module" without a name gets no name again.
//
// デバッグビルドの #module ブロックを組み直す。hspcmp は "#module 名前" を
// "goto *_名前_exit" に、"#global" を "*_名前_exit" に展開し(token.cpp の PP_Module と
// PP_Global)、デバッグビルドはそのラベル名を記録する。そこでこの goto とラベルを #module と
// #global に戻す。中の名前はモジュールのスコープを取り戻し(ModuleScope)、メンバーを持つ
// モジュールは #struct 行を落とす。名前の無い "#module" に hspcmp が付けた "m0"・"m1" などの
// モジュールは再び名前無しにする。
internal static class ModuleBlocks
{
    private const string AnonymousPrefix = "m";

    internal static void Rebuild(List<LogicalLine> lines, AxData data)
    {
        var exits = new Dictionary<Label, string>();
        foreach (Label label in data.Labels)
        {
            if (DebugNames.ExitModule(data, label) is { } name)
            {
                exits[label] = name;
            }
        }

        if (exits.Count == 0)
        {
            return;
        }

        var blocks = new List<Block>();
        int from = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            if (
                lines[i] is PreprocessorDeclaration { Declared: Label { Function: null } exit }
                && exits.TryGetValue(exit, out string? name)
                && FindStart(lines, from, i, exit) is { } start
            )
            {
                blocks.Add(new Block(name, start, i, lines[start].TokenOffset, exit.TokenOffset));
                from = i + 1;
            }
        }

        if (blocks.Count != 0)
        {
            Apply(lines, data, blocks);
        }
    }

    private static int? FindStart(List<LogicalLine> lines, int from, int end, Label exit)
    {
        string jump = "goto " + exit.LabelName;
        for (int j = end - 1; j >= from; j--)
        {
            if (lines[j] is Command && lines[j].TokenOffset >= 0 && lines[j].ToString() == jump)
            {
                return j;
            }
        }

        return null;
    }

    private static void Apply(List<LogicalLine> lines, AxData data, List<Block> blocks)
    {
        foreach (Function function in data.Functions.Where(f => f.IsUserFunction))
        {
            if (function.FunctionName is { } name && !name.Contains('@', StringComparison.Ordinal))
            {
                data.Scope.AddGlobalFunction(name);
            }
        }

        var structs = new HashSet<Function>();
        int anonymous = 0;
        foreach (Block block in blocks)
        {
            bool isAnonymous = block.Name == AnonymousPrefix + anonymous;
            anonymous += isAnonymous ? 1 : 0;
            Function? members = data.Modules.Find(m =>
                string.Equals(m.FunctionName, block.Name, StringComparison.OrdinalIgnoreCase)
            );
            if (members != null)
            {
                structs.Add(members);
            }

            lines[block.Start] = new ModuleStart(
                block.Name,
                isAnonymous,
                members,
                block.StartOffset
            );
            lines[block.End] = new EndOfModule(block.EndOffset);
            data.Scope.HasUnusedModule |= !IsUsed(data, block, members);
        }

        AssignOwners(data, blocks);
        lines.RemoveAll(line =>
            line is PreprocessorDeclaration { Declared: Function module }
            && structs.Contains(module)
        );
    }

    // hspcmp's optcode drops a module that nothing refers to: no call of a function it
    // declares and no use of its name (codegen.cpp GenerateCodePP_module), so such a module
    // means optcode was off.
    //
    // hspcmp の optcode は、宣言した関数の呼び出しも名前の使用も無いモジュールを削除する
    // (codegen.cpp の GenerateCodePP_module)。そのため、そのようなモジュールがあれば optcode は
    // 切られていた。
    private static bool IsUsed(AxData data, Block block, Function? members) =>
        members?.FirstUseOffset >= 0
        || data.Functions.Exists(f =>
            f.IsUserFunction
            && f.FirstUseOffset >= 0
            && f.Label is { } label
            && block.Holds(label.TokenOffset)
        );

    // A recorded name says which module a label belongs to; a label without one belongs to
    // the block its code lies in.
    //
    // 記録された名前があればラベルが属するモジュールはそれで分かる。名前の無いラベルは、その
    // コードがあるブロックに属する。
    private static void AssignOwners(AxData data, List<Block> blocks)
    {
        HashSet<string> names = new(blocks.Select(b => b.Name), StringComparer.OrdinalIgnoreCase);
        foreach (Label label in data.Labels)
        {
            label.Scope = data.Scope;
            bool recorded = data.DebugInfo.LabelNames.ContainsKey(label.Index);
            string? owner = recorded
                ? DebugNames.RecordedModule(data, label)
                : blocks.Find(b => b.Holds(label.TokenOffset))?.Name;
            label.Owner = owner != null && names.Contains(owner) ? owner : null;
        }
    }

    private sealed record Block(string Name, int Start, int End, int StartOffset, int EndOffset)
    {
        internal bool Holds(int offset) => offset >= StartOffset && offset < EndOffset;
    }
}
