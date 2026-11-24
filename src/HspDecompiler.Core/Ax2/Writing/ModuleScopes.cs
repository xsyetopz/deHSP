using System.Collections.Generic;
using System.Linq;

namespace HspDecompiler.Core.Ax2.Writing;

// The #module spans hspcmp 2.5 and 2.55 record, and the scope each variable and #func name
// belongs to. A name declared inside a module is that module's own, so a name from elsewhere
// takes "@" for the global scope or "@module" for another module. The AX keeps only indices, so
// a variable used outside every module is taken as global, one used only in modules belongs to
// the first module that uses it, and a #func belongs to the module its #func line lands in.
//
// hspcmp 2.5 と 2.55 が記録する #module の範囲と、各変数と #func の名前が属するスコープ。
// モジュール内で宣言した名前はそのモジュール自身のものであるため、他所の名前にはグローバルなら
// "@"、別のモジュールなら "@モジュール" を付ける。AX は番号しか残さないため、どのモジュールの
// 外でも使われる変数はグローバルとし、モジュール内だけで使われる変数は最初に使うモジュールの
// ものとし、#func はその #func の行が入るモジュールのものとする。
internal sealed class ModuleScopes
{
    private readonly List<(int Start, int End, string Name)> _spans = [];
    private readonly Dictionary<int, string?> _owners = [];
    private readonly Dictionary<int, string?> _funcOwners = [];

    // A module covers the tokens from its jump up to the token its #global label marks.
    //
    // モジュールは、その goto から #global のラベルが指すトークンの手前までのトークンを覆う。
    internal void Add(int start, int end, string name) => _spans.Add((start, end, name));

    internal string? At(int tokenId)
    {
        foreach ((int start, int end, string name) in _spans)
        {
            if ((tokenId >= start) && (tokenId < end))
            {
                return name;
            }
        }

        return null;
    }

    // Sets each variable's scope from its uses, given as (token id, variable index) in token
    // order.
    //
    // 各変数のスコープを、トークン順の (トークンの番号, 変数の番号) で与えた使用から決める。
    internal void Own(IEnumerable<(int Id, int Variable)> uses)
    {
        if (_spans.Count == 0)
        {
            return;
        }

        foreach (IGrouping<int, (int Id, int Variable)> variable in uses.GroupBy(u => u.Variable))
        {
            List<string?> scopes = [.. variable.Select(u => At(u.Id))];
            _owners[variable.Key] = scopes.Contains(null) ? null : scopes[0];
        }
    }

    // IncludeLayout.Merge puts a #func line after the first anchored line whose successor's
    // anchor lies past the #func name, or before every line when the first anchor does; the
    // #func belongs to that line's module. Lines are (token id, anchor) in output order, with -1
    // for a line without an anchor, and nameOffsets are the #func names by index.
    //
    // IncludeLayout.Merge は #func の行を、次の行のアンカーが #func の名前より後ろにある最初の
    // アンカー付きの行の後ろに置き、最初のアンカーがそうならすべての行の前に置く。#func はその
    // 行のモジュールに属する。lines は出力順の (トークンの番号, アンカー) で、アンカーの無い行は
    // -1、nameOffsets は番号順の #func の名前である。
    internal void OwnFuncs(List<(int Id, int Anchor)> lines, IEnumerable<int> nameOffsets)
    {
        if (_spans.Count == 0)
        {
            return;
        }

        List<(int Id, int Anchor)> anchored = [.. lines.Where(l => l.Anchor >= 0)];
        int index = 0;
        foreach (int offset in nameOffsets)
        {
            int after = anchored.FindIndex(l => l.Anchor > offset) - 1;
            int landing = after < -1 ? anchored.Count - 1 : after;
            _funcOwners[index] = landing < 0 ? null : At(anchored[landing].Id);
            index++;
        }
    }

    // The suffix a variable takes when written inside the given module, or outside every
    // module when module is null.
    //
    // 指定したモジュールの中(module が null ならどのモジュールの外)で変数を書く時に付ける接尾辞。
    internal string VariableSuffix(int variable, string? module) =>
        Suffix(_owners.GetValueOrDefault(variable), module);

    internal string FuncSuffix(int func, string? module) =>
        Suffix(_funcOwners.GetValueOrDefault(func), module);

    private static string Suffix(string? owner, string? module) =>
        owner == module ? "" : "@" + owner;
}
