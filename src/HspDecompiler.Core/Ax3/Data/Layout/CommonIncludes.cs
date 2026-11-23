using System.Collections.Generic;
using System.IO;
using System.Linq;
using HspDecompiler.Core.Ax3.Data.PP;
using HspDecompiler.Core.Encoding;

namespace HspDecompiler.Core.Ax3.Data.Layout;

// Restores the #include of a common header (hspcmp.as, a Windows API header) that left no code
// of its own, given the directory that holds the caller's copy. The imports the program took
// from it are matched by DLL, export name and parameter types; they get the header's aliases
// and are no longer declared in the main source, so the recompile reads them from the header
// as the original did. A header is only restored when each import it could have declared
// matches it exactly and none of its names clash with a name of the output, because either
// would stop the recompile or bind a name to another symbol.
//
// コードを残さなかった共通ヘッダー(hspcmp.as や Windows API のヘッダー)の #include を、
// 呼び出し側が持つコピーのディレクトリから復元する。プログラムがそこから取り込んだインポート
// は DLL・エクスポート名・引数の型で照合し、ヘッダーの別名を付けてメインのソースでは宣言し
// ない。そのため再コンパイルは元と同じくヘッダーから読む。ヘッダーを復元するのは、宣言し得た
// 各インポートが完全に一致し、ヘッダーの名前が出力の名前と衝突しない時だけである。どちらかが
// 崩れると再コンパイルが止まるか、名前が別のシンボルに結び付くためである。
internal static class CommonIncludes
{
    internal static HashSet<int> Attach(AxData data, IncludeNames names, string? directory)
    {
        var restored = new HashSet<int>();
        if (string.IsNullOrEmpty(directory))
        {
            return restored;
        }

        HashSet<int> codeFiles =
        [
            .. data.DebugInfo.Lines.Where(line => line.CodeSize > 0).Select(line => line.File),
        ];
        for (int file = names.MainFile + 1; file < names.Count; file++)
        {
            if (!names.IsCommon(file) || codeFiles.Contains(file))
            {
                continue;
            }

            string path = Path.Combine(
                directory,
                names.RelativeName(file).Replace('\\', Path.DirectorySeparatorChar)
            );
            if (
                File.Exists(path)
                && TryAttach(data, CommonHeader.Parse(Decode(File.ReadAllBytes(path))))
            )
            {
                restored.Add(file);
            }
        }

        return restored;
    }

    private static bool TryAttach(AxData data, CommonHeader header)
    {
        var matches = new Dictionary<Function, string>();
        foreach (
            Function function in data.Functions.Where(f => f.IsDllFunction && !f.DeclaredByInclude)
        )
        {
            if (function.Dll?.Name is not { } dll || !header.Dlls.Contains(dll))
            {
                continue;
            }

            List<ImportDeclaration> candidates =
            [
                .. header.Imports.Where(i => i.Export == function.DefaultName),
            ];
            if (candidates.Count == 0)
            {
                continue;
            }

            var own = function.ToImportDeclaration();
            ImportDeclaration? match = candidates.Find(c =>
                own != null && c.Signature == own.Signature
            );
            if (match == null)
            {
                return false;
            }

            matches[function] = match.Alias;
        }

        if (Clashes(data, header, matches))
        {
            return false;
        }

        foreach ((Function function, string alias) in matches)
        {
            function.SetName(alias);
            function.DeclaredByInclude = true;
        }

        return true;
    }

    private static bool Clashes(
        AxData data,
        CommonHeader header,
        Dictionary<Function, string> matches
    )
    {
        IEnumerable<string?> variables = Enumerable
            .Range(0, int.MaxValue)
            .Select(data.GetVariableName)
            .TakeWhile(name => name != null);
        IEnumerable<string?> functions = data
            .Functions.Where(f => !matches.ContainsKey(f) && !f.DeclaredByInclude)
            .Select(f => f.FunctionName);
        return variables
            .Concat(functions)
            .Any(name => name != null && header.Names.Contains(name.ToLowerInvariant()));
    }

    // hspcmp reads a common header in the encoding of the main source; the headers that ship
    // with HSP are Shift_JIS, and their declarations are ASCII either way.
    //
    // hspcmp は共通ヘッダーをメインのソースと同じエンコーディングで読む。HSP に付属する
    // ヘッダーは Shift_JIS で、宣言はどちらでも ASCII である。
    private static string Decode(byte[] bytes) =>
        System.Text.Unicode.Utf8.IsValid(bytes)
            ? System.Text.Encoding.UTF8.GetString(bytes)
            : ShiftJisHelper.Encoding.GetString(bytes);
}
