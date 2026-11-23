using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HspDecompiler.Core.Ax3.Data.Layout;

// One #func or #cfunc line, reduced to what reaches the AX: the keyword, onexit, the export
// name and the parameter types. The alias is kept apart because the AX does not store it.
//
// 1 行の #func か #cfunc を AX に届く部分(キーワード・onexit・エクスポート名・引数の型)に
// 絞ったもの。別名は AX に残らないため分けて持つ。
internal sealed record ImportDeclaration(string Alias, string Signature, string Export)
{
    // A single number is the old type code; "$202" and "514" are the same code.
    //
    // 数値 1 つは古い形式の型コードで、"$202" と "514" は同じコードである。
    internal static ImportDeclaration Of(
        string alias,
        string keyword,
        bool onExit,
        string export,
        IEnumerable<string> parameters
    )
    {
        List<string> parts = [.. parameters.Select(token => token.ToLowerInvariant())];
        string normalized =
            parts.Count == 1 && TryParseNumber(parts[0], out int code)
                ? code.ToString(CultureInfo.InvariantCulture)
                : string.Join(',', parts);
        string signature = string.Join(
            ' ',
            keyword.ToLowerInvariant(),
            onExit ? "onexit" : "-",
            export,
            normalized
        );
        return new ImportDeclaration(alias, signature, export);
    }

    private static bool TryParseNumber(string token, out int value)
    {
        int prefix =
            token.StartsWith('$') ? 1
            : token.StartsWith("0x", StringComparison.Ordinal) ? 2
            : 0;
        NumberStyles style = prefix == 0 ? NumberStyles.None : NumberStyles.HexNumber;
        return int.TryParse(token[prefix..], style, CultureInfo.InvariantCulture, out value);
    }
}

// The declarations of a common header such as hspcmp.as, read without running its #ifdef
// blocks: both branches of "#ifdef __hsp64__" count, so each export can match whichever
// branch the program was built with. Comments are skipped.
//
// hspcmp.as のような共通ヘッダーの宣言を、#ifdef ブロックを実行せずに読む。
// "#ifdef __hsp64__" の両方の分岐を数えるため、各エクスポートはプログラムがどちらの分岐で
// ビルドされていても一致できる。コメントは読み飛ばす。
internal sealed class CommonHeader
{
    private static readonly string[] s_importKeywords = ["#func", "#cfunc"];

    internal HashSet<string> Dlls { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal List<ImportDeclaration> Imports { get; } = [];

    // Every name the header defines (#define, #func, #cfunc and the like), lowercased.
    //
    // ヘッダーが定義するすべての名前(#define・#func・#cfunc など)を小文字にしたもの。
    internal HashSet<string> Names { get; } = new(StringComparer.Ordinal);

    internal static CommonHeader Parse(string text)
    {
        var header = new CommonHeader();
        bool inBlock = false;
        foreach (string rawLine in text.Split('\n'))
        {
            List<string> tokens = Tokenize(rawLine, ref inBlock);
            if (tokens.Count != 0)
            {
                header.Add(tokens);
            }
        }

        return header;
    }

    private void Add(List<string> tokens)
    {
        string keyword = tokens[0].ToLowerInvariant();
        if (keyword == "#uselib" && tokens.Count > 1)
        {
            Dlls.Add(Unquote(tokens[1]));
            return;
        }

        if (ReadImport(tokens) is { } import)
        {
            Imports.Add(import);
        }

        if (keyword.StartsWith('#') && DefinedName(tokens) is { } name)
        {
            Names.Add(name.ToLowerInvariant());
        }
    }

    private static ImportDeclaration? ReadImport(List<string> tokens)
    {
        if (tokens.Count < 3 || !s_importKeywords.Contains(tokens[0].ToLowerInvariant()))
        {
            return null;
        }

        int at = 1;
        if (tokens[at].Equals("global", StringComparison.OrdinalIgnoreCase))
        {
            at++;
        }

        string alias = tokens[at++];
        bool onExit =
            at < tokens.Count && tokens[at].Equals("onexit", StringComparison.OrdinalIgnoreCase);
        at += onExit ? 1 : 0;
        if (at >= tokens.Count)
        {
            return null;
        }

        string export = Unquote(tokens[at++]);
        return ImportDeclaration.Of(alias, tokens[0], onExit, export, tokens.Skip(at));
    }

    // The name after a defining keyword, skipping "global", "ctype" and "local" modifiers.
    //
    // 定義キーワードの後の名前。"global"・"ctype"・"local" の修飾は飛ばす。
    private static string? DefinedName(List<string> tokens)
    {
        string[] defining =
        [
            "#define",
            "#func",
            "#cfunc",
            "#deffunc",
            "#defcfunc",
            "#cmd",
            "#const",
            "#enum",
        ];
        if (!defining.Contains(tokens[0].ToLowerInvariant()))
        {
            return null;
        }

        string[] modifiers = ["global", "ctype", "local", "double", "int"];
        return tokens
            .Skip(1)
            .FirstOrDefault(token => !modifiers.Contains(token.ToLowerInvariant()));
    }

    private static string Unquote(string token) =>
        token.Length >= 2 && token[0] == '"' && token[^1] == '"' ? token[1..^1] : token;

    // Splits a line at spaces, tabs and commas. A quoted string stays one token, and ';', "//"
    // and "/* */" outside one start a comment.
    //
    // 行を空白・タブ・カンマで区切る。引用符で囲んだ文字列は 1 つのトークンのままにし、その外の
    // ';'・"//"・"/* */" はコメントの始まりとする。
    private static List<string> Tokenize(string line, ref bool inBlock)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            if (inBlock)
            {
                inBlock = !(line[i] == '*' && Next(line, i) == '/');
                i += inBlock ? 0 : 1;
                continue;
            }

            char c = line[i];
            if (c == '"')
            {
                i = ReadQuoted(line, i, current);
            }
            else if (c == ';' || (c == '/' && Next(line, i) == '/'))
            {
                break;
            }
            else if (c == '/' && Next(line, i) == '*')
            {
                inBlock = true;
                i++;
            }
            else if (char.IsWhiteSpace(c) || c == ',')
            {
                EndToken(tokens, current);
            }
            else
            {
                current.Append(c);
            }
        }

        EndToken(tokens, current);
        return tokens;
    }

    private static char Next(string line, int i) => i + 1 < line.Length ? line[i + 1] : '\0';

    private static int ReadQuoted(string line, int start, StringBuilder current)
    {
        current.Append('"');
        int i = start + 1;
        while (i < line.Length && line[i] != '"')
        {
            if (line[i] == '\\' && i + 1 < line.Length)
            {
                current.Append(line[i++]);
            }

            current.Append(line[i++]);
        }

        current.Append('"');
        return i;
    }

    private static void EndToken(List<string> tokens, StringBuilder current)
    {
        if (current.Length != 0)
        {
            tokens.Add(current.ToString());
            current.Clear();
        }
    }
}
