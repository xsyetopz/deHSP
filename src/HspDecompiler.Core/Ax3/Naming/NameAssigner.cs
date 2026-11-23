using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Ax3.Data.PP;

namespace HspDecompiler.Core.Ax3.Naming;

// The names a decompiled AX3 program gets where the AX keeps none: user functions, DLL and COM
// imports, and labels. Every name is checked against the keywords and the names given before
// it, because HSP names ignore case and a collision binds a different symbol on recompile.
//
// AX に名前が残らない所で、復元する AX3 プログラムに付ける名前。ユーザー関数、DLL と COM の
// インポート、ラベル。HSP の名前は大文字と小文字を区別せず、衝突すると再コンパイルで別の
// シンボルに結び付くため、どの名前もキーワードと先に付けた名前に照らして確かめる。
internal static class NameAssigner
{
    private static readonly CompositeFormat s_userFunctionNameFormat = CompositeFormat.Parse(
        "{0}_{1}"
    );

    private static readonly CompositeFormat s_dllFunctionNameFormat = CompositeFormat.Parse(
        "func_{0}"
    );

    private static readonly CompositeFormat s_comFunctionNameFormat = CompositeFormat.Parse(
        "comfunc_{0}"
    );

    // Upper bound for OpenProject_dll2..OpenProject_dllN; past it, func_N reads better.
    //
    // OpenProject_dll2〜OpenProject_dllN の上限。これを超えるなら func_N の方が読みやすい。
    private const int MaxDllAliasSuffix = 9;

    // Global user functions keep their own names first, then module members, DLL imports and
    // COM imports take what is left, in that order.
    //
    // まずグローバルなユーザー関数が自分の名前を使い、残りをモジュールのメンバー、DLL の
    // インポート、COM のインポートの順に取る。
    internal static void NameFunctions(IEnumerable<Function> functions, Hsp3Dictionary dictionary)
    {
        var functionNames = new List<string>();
        var initializer = new List<Function>();
        var comfuncs = new List<Function>();
        var dllfuncs = new List<Function>();
        functionNames.AddRange(dictionary.GetAllFuncName());
        foreach (Function func in functions)
        {
            switch (func.Type)
            {
                case FunctionType.CFunc:
                case FunctionType.Func:
                    dllfuncs.Add(func);
                    break;
                case FunctionType.ComFunc:
                    comfuncs.Add(func);
                    break;
                case FunctionType.DefCFunc:
                case FunctionType.DefFunc:
                case FunctionType.Module:
                    if (func.ParentModule != null)
                    {
                        initializer.Add(func);
                    }
                    else
                    {
                        func.SetName(func.DefaultName ?? "");
                        functionNames.Add(NameKey(func.DefaultName ?? ""));
                    }

                    break;
                case FunctionType.None:
                default:
                    break;
            }
        }

        NameUserFunctions(functionNames, initializer);
        NameDllFunctions(functionNames, dllfuncs);
        NameComFunctions(functionNames, comfuncs);
    }

    // The .ax file stores only the DLL export name, not the alias the author wrote, so the
    // alias comes from the export. HSP names ignore case, and wrapper scripts often define
    // "#deffunc openproject" around an "_OpenProject@8" import, so the bare name collides.
    // The candidates, in order: the undecorated name (OpenProject), the export without the
    // stdcall suffix (_OpenProject), OpenProject_dll, OpenProject_dll2 to OpenProject_dll9
    // for repeated imports, and last the numbered func_N.
    //
    // .ax ファイルには作者が付けた別名ではなく DLL のエクスポート名しか残らないため、別名は
    // エクスポート名から作る。HSP の名前は大文字と小文字を区別せず、ラッパースクリプトは
    // "_OpenProject@8" のインポートを "#deffunc openproject" で包むことが多いので、素の名前は
    // 衝突しやすい。候補は順に、装飾を外した名前(OpenProject)、stdcall の接尾辞だけを外した
    // 名前(_OpenProject)、OpenProject_dll、重複インポート用の OpenProject_dll2〜
    // OpenProject_dll9、最後に番号付きの func_N。
    internal static string ResolveDllFunctionName(
        string exportName,
        ICollection<string> takenLowerNames
    )
    {
        int atIndex = exportName.IndexOf('@');
        string undecorated = atIndex > 0 ? exportName[..atIndex] : exportName;
        string bare =
            undecorated.StartsWith('_') && undecorated.Length > 1 ? undecorated[1..] : undecorated;

        var candidates = new List<string>();
        if (IsHspIdentifier(bare))
        {
            candidates.Add(bare);
            if (undecorated != bare)
            {
                candidates.Add(undecorated);
            }

            candidates.Add(bare + "_dll");
            // Flattened modules each import the same export (CloseHandle in three #module
            // blocks), so repeats get a number per export.
            //
            // 展開されたモジュールがそれぞれ同じエクスポートをインポートする(3 つの #module に
            // CloseHandle がある等)ため、重複にはエクスポートごとに番号を付ける。
            for (int n = 2; n <= MaxDllAliasSuffix; n++)
            {
                candidates.Add(string.Create(CultureInfo.InvariantCulture, $"{bare}_dll{n}"));
            }
        }

        foreach (string candidate in candidates)
        {
            if (IsHspIdentifier(candidate) && !takenLowerNames.Contains(NameKey(candidate)))
            {
                return candidate;
            }
        }

        int index = 1;
        string numbered;
        do
        {
            numbered = string.Format(CultureInfo.InvariantCulture, s_dllFunctionNameFormat, index);
            index++;
        } while (takenLowerNames.Contains(numbered));

        return numbered;
    }

    // A label the debug segment named keeps that name; the rest are numbered in code order,
    // skipping numbers whose name a recovered label already took.
    //
    // デバッグセグメントが名付けたラベルはその名前を使う。残りはコード順に番号を振り、
    // 復元したラベルが既に使った名前の番号は飛ばす。
    internal static void NameLabels(AxData data)
    {
        List<Label> labels = data.Labels;
        if (labels.Count <= 0)
        {
            return;
        }

        labels.Sort();
        Dictionary<int, string> recovered = DebugNames.LabelNames(data, labels);
        var taken = new HashSet<string>(
            recovered.Values.Select(name => name.ToLowerInvariant()),
            StringComparer.Ordinal
        );
        int keta = ((int)Math.Log10(labels.Count)) + 1;
        var labelFormat = CompositeFormat.Parse(
            "*label_{0:D0" + keta.ToString(CultureInfo.InvariantCulture) + "}"
        );
        int number = 0;
        foreach (Label label in labels)
        {
            if (recovered.TryGetValue(label.Index, out string? name))
            {
                label.LabelName = name;
                continue;
            }

            string generated;
            do
            {
                generated = string.Format(CultureInfo.InvariantCulture, labelFormat, number++);
            } while (taken.Contains(generated));

            label.LabelName = generated;
        }
    }

    private static void NameUserFunctions(List<string> functionNames, List<Function> initializer)
    {
        foreach (Function func in initializer)
        {
            string defName = func.DefaultName ?? "";
            if (!functionNames.Contains(NameKey(defName)))
            {
                func.SetName(defName);
                functionNames.Add(NameKey(defName));
                continue;
            }

            string newName;
            int index = 1;
            do
            {
                newName = string.Format(
                    CultureInfo.InvariantCulture,
                    s_userFunctionNameFormat,
                    defName,
                    index
                );
                index++;
            } while (functionNames.Contains(NameKey(newName)));

            func.SetName(newName);
            functionNames.Add(NameKey(newName));
        }
    }

    private static void NameDllFunctions(List<string> functionNames, List<Function> dllfuncs)
    {
        foreach (Function func in dllfuncs)
        {
            string newName = ResolveDllFunctionName(func.DefaultName ?? "", functionNames);
            func.SetName(newName);
            functionNames.Add(NameKey(newName));
        }
    }

    private static void NameComFunctions(List<string> functionNames, List<Function> comfuncs)
    {
        foreach (Function func in comfuncs)
        {
            string newName;
            int index = 1;
            do
            {
                newName = string.Format(
                    CultureInfo.InvariantCulture,
                    s_comFunctionNameFormat,
                    index
                );
                index++;
            } while (functionNames.Contains(NameKey(newName)));

            func.SetName(newName);
            functionNames.Add(NameKey(newName));
        }
    }

    // HSP names ignore case. The dictionary lowers its names the same invariant way, so both
    // lists compare equal under any UI culture.
    //
    // HSP の名前は大文字と小文字を区別しない。辞書も同じく不変カルチャで小文字にするため、
    // UI のカルチャに関係なく両方の一覧が一致する。
    private static string NameKey(string name) => name.ToLowerInvariant();

    // Ordinal exports ("#12") and C++ mangled names ("?Fn@@YAXH@Z") are not valid HSP names,
    // so they fall through to func_N.
    //
    // 序数のエクスポート("#12")や C++ の装飾名("?Fn@@YAXH@Z")は HSP の名前として無効な
    // ため、func_N に回す。
    private static bool IsHspIdentifier(string name) =>
        name.Length > 0
        && name.Length <= 59
        && (char.IsLetter(name[0]) || name[0] == '_')
        && name.All(c => char.IsLetterOrDigit(c) || c == '_');
}
