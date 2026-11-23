using System;
using System.Collections.Generic;
using HspDecompiler.Core.Ax3.Data.Line;

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

    internal string? Current { get; private set; }

    // Set when a rebuilt module has no use that keeps it under hspcmp's optcode.
    //
    // 再構築したモジュールに、hspcmp の optcode で残す理由となる使用が無い時に立てる。
    internal bool HasUnusedModule { get; set; }

    internal void AddGlobalFunction(string name) => _globalFunctions.Add(name);

    // Renders one line in order, entering a module at its #module and leaving it at #global.
    //
    // 行を順に出力し、#module でモジュールに入り #global で出る。
    internal string Render(LogicalLine line)
    {
        if (line is EndOfModule)
        {
            Current = null;
        }

        string text = line.ToString();
        if (line is ModuleStart start)
        {
            Current = start.Name;
        }

        return text;
    }

    // A module variable whose bare name a global function holds keeps its suffix, because the
    // bare name would read as that function.
    //
    // 裸の名前をグローバル関数が持つモジュール変数は接尾辞を残す。裸の名前はその関数と読まれる
    // ためである。
    internal string Variable(string name)
    {
        if (Current == null)
        {
            return name;
        }

        int at = name.IndexOf('@', StringComparison.Ordinal);
        if (at < 0)
        {
            return name + "@";
        }

        string bare = name[..at];
        return IsCurrent(name[(at + 1)..]) && !_globalFunctions.Contains(bare) ? bare : name;
    }

    internal string Function(string name)
    {
        int at = name.IndexOf('@', StringComparison.Ordinal);
        return at >= 0 && IsCurrent(name[(at + 1)..]) ? name[..at] : name;
    }

    internal string Label(string name, string? owner) =>
        owner == null ? (Current == null ? name : name + "@")
        : IsCurrent(owner) ? name
        : name + "@" + owner;

    private bool IsCurrent(string module) =>
        Current != null && module.Equals(Current, StringComparison.OrdinalIgnoreCase);
}
