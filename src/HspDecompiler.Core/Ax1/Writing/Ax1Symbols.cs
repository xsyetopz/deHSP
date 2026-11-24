using System;
using System.Collections.Generic;
using System.Globalization;

namespace HspDecompiler.Core.Ax1.Writing;

// The names the output gives variables and labels, created in the order ASC numbers them. ASC
// gives a new name the next variable index where it first appears, so a use of the next index
// becomes v{n}. Any other index, such as one past the header's count, which only a define can
// name, gets an alias d{index} defined before its first statement. Labels take indices in the
// order of their first definition or reference, and l{k} is the label of index k.
//
// 出力が変数とラベルに付ける名前。ASC が番号を付ける順に作る。ASC は名前が最初に現れた所で
// 次の変数の番号を与えるため、次の番号の使用は v{n} になる。それ以外の番号(ヘッダーの数を
// 超え、define だけが名前を付けられる番号など)は、最初の文の前で定義する別名 d{番号} を得る。
// ラベルは最初の定義または参照の順に番号を取り、l{k} は番号 k のラベルである。
internal sealed class Ax1Symbols(int first, int globalCount, Func<int, string?> systemName)
{
    private readonly int _first = first;
    private readonly HashSet<int> _aliases = [];
    private readonly Dictionary<int, string> _labelsAt = [];
    private readonly List<string> _labels = [];

    internal int NextGlobal { get; private set; } = first;

    internal int NextLocal { get; private set; } = first;

    internal string Global(int index, bool creates, List<string> prefix)
    {
        if (index < _first && systemName(index) is { } name)
        {
            return name;
        }

        if (index >= _first && index < NextGlobal)
        {
            return Numbered("v", index - _first);
        }

        if (creates && index == NextGlobal && index < globalCount)
        {
            NextGlobal++;
            return Numbered("v", index - _first);
        }

        string alias = Numbered("d", index);
        if (_aliases.Add(index))
        {
            prefix.Add("define " + alias + " " + index.ToString(CultureInfo.InvariantCulture));
        }

        return alias;
    }

    // A local's name, and false when ASC could not have numbered it there.
    //
    // ローカル変数の名前。ASC がそこでその番号を付けられなかった時は false も返す。
    internal (string Name, bool InOrder) Local(int index, bool creates)
    {
        bool inOrder = index < NextLocal || (creates && index == NextLocal);
        if (creates && index == NextLocal)
        {
            NextLocal++;
        }

        return (Numbered("vl", index - _first), inOrder);
    }

    internal string DefineAt(int offset) => "*" + At(offset).Name;

    internal (string Name, bool IsNew) At(int offset)
    {
        if (_labelsAt.TryGetValue(offset, out string? name))
        {
            return (name, false);
        }

        name = Create();
        _labelsAt[offset] = name;
        return (name, true);
    }

    // The label of index k. Any index skipped on the way gets a filler label defined before the
    // statement; a resolved image defines the label there too, since an undefined label would
    // leave every index unresolved. The result is false when a filler has to follow a label
    // the statement already created, which no prefix can do.
    //
    // 番号 k のラベル。途中で飛ばした番号は文の前で定義する埋め草のラベルを得る。解決済みの
    // イメージではラベルもそこで定義する。未定義のラベルがあるとすべての番号が未解決のまま残る
    // ためである。文が既にラベルを作った後に埋め草が要る時は false を返す。前置きではそれが
    // できない。
    internal (string Name, bool InOrder) Indexed(
        int k,
        bool resolved,
        List<string> prefix,
        ref bool created
    )
    {
        if (k < _labels.Count)
        {
            return (_labels[k], true);
        }

        bool inOrder = !created || k == _labels.Count;
        while (_labels.Count < k)
        {
            prefix.Add("*" + Create());
        }

        string name = Create();
        if (resolved)
        {
            prefix.Add("*" + name);
        }
        else
        {
            created = true;
        }

        return (name, inOrder);
    }

    private string Create()
    {
        string name = Numbered("l", _labels.Count);
        _labels.Add(name);
        return name;
    }

    private static string Numbered(string prefix, int n) =>
        prefix + n.ToString(CultureInfo.InvariantCulture);
}
