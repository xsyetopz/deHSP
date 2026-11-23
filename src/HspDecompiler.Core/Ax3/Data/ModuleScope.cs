using System;
using System.Collections.Generic;

namespace HspDecompiler.Core.Ax3.Data;

// Spells names for the module the output line sits in. hspcmp appends "@module" to each name
// it reads inside a #module block that is not a global symbol, and records variables and
// labels under that full name. So inside the block its own names go bare and a global variable
// or label takes a bare "@"; outside, a module name keeps its suffix. Functions declared with
// #deffunc are global symbols and need no "@" from inside another module.
//
// 出力行が属するモジュールに合わせて名前を書く。hspcmp は #module ブロックの中で読んだ
// グローバルシンボル以外の名前に "@モジュール" を付け、変数とラベルをその完全な名前で記録
// する。そのためブロックの中では自身の名前を裸で書き、グローバルの変数とラベルには "@" だけを
// 付ける。外ではモジュールの名前は接尾辞を残す。#deffunc で宣言した関数はグローバルシンボル
// のため、他のモジュールの中からも "@" は要らない。
internal sealed class ModuleScope
{
    private readonly HashSet<string> _globalFunctions = new(StringComparer.OrdinalIgnoreCase);

    internal void AddGlobalFunction(string name) => _globalFunctions.Add(name);

    internal IReadOnlyCollection<string> GlobalFunctions => _globalFunctions;

    // Returns to the given global functions, as before a layout that failed.
    //
    // 失敗した配置の前のように、与えたグローバル関数に戻る。
    internal void Reset(IEnumerable<string> globalFunctions)
    {
        _globalFunctions.Clear();
        _globalFunctions.UnionWith(globalFunctions);
    }

    // A module variable whose bare name a global function holds keeps its suffix, because the
    // bare name would read as that function.
    //
    // 裸の名前をグローバル関数が持つモジュール変数は接尾辞を残す。裸の名前はその関数と読まれる
    // ためである。
    internal string Variable(string name, string? module)
    {
        if (module == null)
        {
            return name;
        }

        int at = name.IndexOf('@', StringComparison.Ordinal);
        if (at < 0)
        {
            return name + "@";
        }

        string bare = name[..at];
        return Is(module, name[(at + 1)..]) && !_globalFunctions.Contains(bare) ? bare : name;
    }

    internal static string Function(string name, string? module)
    {
        int at = name.IndexOf('@', StringComparison.Ordinal);
        return at >= 0 && Is(module, name[(at + 1)..]) ? name[..at] : name;
    }

    // An import resolves inside a module only through the global scope: hspcmp 3.1 to 3.8
    // reject a bare call there to a #func declared outside, while "name@" builds the same bytes
    // as a "#func global" declaration.
    //
    // インポートはモジュール内ではグローバルスコープを通してしか解決されない。hspcmp 3.1〜3.8 は
    // 外で宣言した #func をそこで裸で呼ぶとエラーにし、"名前@" は "#func global" 宣言と同じ
    // バイト列になる。
    internal static string Import(string name, string? module) =>
        module == null || name.Contains('@', StringComparison.Ordinal) ? name : name + "@";

    internal static string Label(string name, string? owner, string? module) =>
        owner == null ? (module == null ? name : name + "@")
        : Is(module, owner) ? name
        : name + "@" + owner;

    private static bool Is(string? module, string name) =>
        module != null && name.Equals(module, StringComparison.OrdinalIgnoreCase);
}
