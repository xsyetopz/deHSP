using System;
using System.Collections.Generic;
using HspDecompiler.Core.Ax3.Data.Line;
using HspDecompiler.Core.Ax3.Data.PP;

namespace HspDecompiler.Core.Ax3.Data;

// hspcmp registers a #func at the line where it appears (codegen.cpp GenerateCodePP_func):
// it takes the next function-table slot and appends its name to the literal data. A user
// function takes its slot at its first call or its #deffunc, whichever comes first
// (GenerateCodePP_deffunc fills a slot a forward call reserved), and code literals join the
// literal data at their line. Each import therefore goes before the first later user
// function, its own first call, and the first line that adds literal data past its name, so
// recompiling rebuilds the original finfo, LIBDAT and literal-data order.
//
// hspcmp は #func を記述された行で登録する(codegen.cpp GenerateCodePP_func)。次の関数
// テーブル枠を取り、名前をリテラルデータに追加する。ユーザー定義関数は最初の呼び出しか
// #deffunc の早い方で枠を得て(前方参照で予約した枠を GenerateCodePP_deffunc が埋める)、
// コード中のリテラルはその行でリテラルデータに追加される。そこで各インポートを、後続の
// ユーザー定義関数・自身の最初の呼び出し・名前より後ろのリテラルを追加する最初の行、
// のいずれよりも前に置く。これで再コンパイル時に finfo・LIBDAT・リテラルデータの順序が
// 元と一致する。
internal static class ImportPlacement
{
    internal static void Insert(List<LogicalLine> lines, AxData data, int anchor)
    {
        List<Function> imports = data.Functions.FindAll(f => f.Dll != null && !f.DeclaredByInclude);
        if (imports.Count == 0)
        {
            return;
        }

        int[] targets = ResolveTargets(lines, data, imports, anchor);
        List<List<LogicalLine>> blocks = BuildBlocks(imports, targets);
        int blockIndex = blocks.Count - 1;
        for (int i = imports.Count - 1; i >= 0; i--)
        {
            if (i == 0 || targets[i - 1] != targets[i])
            {
                lines.InsertRange(targets[i], blocks[blockIndex--]);
            }
        }
    }

    private static int[] ResolveTargets(
        List<LogicalLine> lines,
        AxData data,
        List<Function> imports,
        int anchor
    )
    {
        int tail = Math.Max(anchor, lines.Count - 1);
        var codeLines = new CodeLineIndex(lines, tail);
        var dataOrder = new List<(int DataOffset, int Line)>();
        List<(int Slot, int Line)> userFunctions = FindUserFunctions(lines, codeLines, dataOrder);
        foreach ((int dataOffset, int tokenOffset) in data.LiteralAnchors)
        {
            dataOrder.Add((dataOffset, codeLines.Find(tokenOffset)));
        }

        var dataBound = new DataOrderBound(dataOrder, tail);

        int[] targets = new int[imports.Count];
        for (int i = 0; i < imports.Count; i++)
        {
            Function import = imports[i];
            int next = userFunctions.FindIndex(f => f.Slot > import.Index);
            int target = next >= 0 ? userFunctions[next].Line : tail;
            if (import.FirstUseOffset >= 0)
            {
                target = Math.Min(target, codeLines.Find(import.FirstUseOffset));
            }

            if (import.NameOffset >= 0)
            {
                target = Math.Min(target, dataBound.Before(import.NameOffset));
            }

            targets[i] = Math.Max(anchor, target);
        }

        // Slots must stay in order even when a later import is called first.
        //
        // 後の枠のインポートが先に呼ばれても枠の順序を保つ。
        for (int i = imports.Count - 2; i >= 0; i--)
        {
            targets[i] = Math.Min(targets[i], targets[i + 1]);
        }

        return targets;
    }

    private static List<(int Slot, int Line)> FindUserFunctions(
        List<LogicalLine> lines,
        CodeLineIndex codeLines,
        List<(int DataOffset, int Line)> dataOrder
    )
    {
        var userFunctions = new List<(int Slot, int Line)>();
        for (int i = 0; i < lines.Count; i++)
        {
            if (
                lines[i]
                    is not PreprocessorDeclaration { Declared: Label { Function: { } function } }
                || !function.IsUserFunction
            )
            {
                continue;
            }

            int line =
                function.FirstUseOffset < 0
                    ? i
                    : Math.Min(i, codeLines.Find(function.FirstUseOffset));
            userFunctions.Add((function.Index, line));
            if (function.NameOffset >= 0)
            {
                dataOrder.Add((function.NameOffset, i));
            }
        }

        userFunctions.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        return userFunctions;
    }

    private static List<List<LogicalLine>> BuildBlocks(List<Function> imports, int[] targets)
    {
        var blocks = new List<List<LogicalLine>>();
        Usedll? current = null;
        List<LogicalLine>? block = null;
        for (int i = 0; i < imports.Count; i++)
        {
            if (block == null || targets[i] != targets[i - 1])
            {
                block = [];
                blocks.Add(block);
            }

            Function import = imports[i];
            Usedll dll = import.Dll!;
            bool byHeader = dll.IsDeclaredByHeader(import);
            if (byHeader || (dll != current && dll.Type != UsedllType.UseCom))
            {
                block.Add(new PreprocessorDeclaration(dll));
                current = dll;
            }

            if (!byHeader)
            {
                block.Add(new PreprocessorDeclaration(import));
            }

            if (i == imports.Count - 1 || targets[i + 1] != targets[i])
            {
                block.Add(new CommentLine());
            }
        }

        return blocks;
    }

    // Maps a code offset to the index of the line that holds it.
    //
    // コードオフセットを、それを含む行の番号に対応付ける。
    private sealed class CodeLineIndex
    {
        private readonly List<int> _offsets = [];
        private readonly List<int> _lines = [];
        private readonly int _tail;

        internal CodeLineIndex(List<LogicalLine> lines, int tail)
        {
            _tail = tail;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].TokenOffset >= 0)
                {
                    _offsets.Add(lines[i].TokenOffset);
                    _lines.Add(i);
                }
            }
        }

        internal int Find(int tokenOffset)
        {
            int index = _offsets.BinarySearch(tokenOffset);
            if (index < 0)
            {
                index = ~index - 1;
            }

            return index < 0 ? _tail : _lines[index];
        }
    }

    // Finds the first line that adds literal data after a given offset.
    //
    // 指定位置より後ろのリテラルデータを追加する最初の行を求める。
    private sealed class DataOrderBound
    {
        private readonly List<int> _offsets = [];
        private readonly int[] _firstLineFrom;

        internal DataOrderBound(List<(int DataOffset, int Line)> entries, int tail)
        {
            entries.Sort((a, b) => a.DataOffset.CompareTo(b.DataOffset));
            _firstLineFrom = new int[entries.Count + 1];
            _firstLineFrom[entries.Count] = tail;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                _firstLineFrom[i] = Math.Min(entries[i].Line, _firstLineFrom[i + 1]);
            }

            foreach ((int dataOffset, _) in entries)
            {
                _offsets.Add(dataOffset);
            }
        }

        internal int Before(int dataOffset)
        {
            int index = _offsets.BinarySearch(dataOffset + 1);
            if (index < 0)
            {
                index = ~index;
            }

            while (index > 0 && _offsets[index - 1] > dataOffset)
            {
                index--;
            }

            return _firstLineFrom[index];
        }
    }
}
