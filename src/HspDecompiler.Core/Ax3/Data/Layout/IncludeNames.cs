using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using HspDecompiler.Core.Releases;

namespace HspDecompiler.Core.Ax3.Data.Layout;

// Turns the file paths a debug build recorded into #include names and output file names.
// hspcmp records the path it opened: the common directory joined to the name for a common file,
// the name as written for any other. The common directory is the one hspdef.as came from, and a
// name below it or below the main file's directory is written relative to that directory.
//
// デバッグビルドが記録したファイルパスを #include の名前と出力ファイル名にする。hspcmp は
// 開いたパスを記録する。共通ファイルなら共通ディレクトリと名前をつないだもの、それ以外は
// 書かれた名前のままである。共通ディレクトリは hspdef.as を読んだディレクトリで、その下か
// メインファイルのディレクトリの下にある名前はそのディレクトリからの相対で書く。
internal sealed class IncludeNames
{
    private static readonly SearchValues<char> s_badChars = SearchValues.Create(":*?\"<>|");

    private readonly List<string> _paths;
    private readonly string _commonDirectory;
    private readonly string _mainDirectory;
    private readonly Dictionary<int, string> _outputNames = [];

    internal IncludeNames(IReadOnlyList<string> paths, string mainOutputName)
    {
        _paths = [.. paths];
        HeaderFile =
            _paths.Count > 0
            && FileName(_paths[0])
                .Equals(CompilerReleases.ImplicitHeader, StringComparison.OrdinalIgnoreCase)
                ? 0
                : -1;
        MainFile = HeaderFile + 1;
        _commonDirectory = HeaderFile == 0 ? Directory(_paths[0]) : string.Empty;
        _mainDirectory = MainFile < _paths.Count ? Directory(_paths[MainFile]) : string.Empty;
        AssignOutputNames(mainOutputName);
    }

    internal int HeaderFile { get; }

    internal int MainFile { get; }

    internal int Count => _paths.Count;

    internal bool IsCommon(int file) =>
        _commonDirectory.Length != 0
        && file != MainFile
        && _paths[file].StartsWith(_commonDirectory, StringComparison.OrdinalIgnoreCase);

    // The name relative to the directory hspcmp searched, as an #include writes it.
    //
    // hspcmp が探したディレクトリからの相対名。#include に書く形である。
    internal string RelativeName(int file)
    {
        string path = _paths[file];
        return IsCommon(file) ? path[_commonDirectory.Length..]
            : _mainDirectory.Length != 0
            && path.StartsWith(_mainDirectory, StringComparison.OrdinalIgnoreCase)
                ? path[_mainDirectory.Length..]
            : path;
    }

    // Backslashes in an #include name are escapes, so each one is doubled.
    //
    // #include の名前の中の円記号はエスケープになるため、それぞれ二重にする。
    internal string IncludeText(int file) =>
        _outputNames.TryGetValue(file, out string? output) && !IsCommon(file)
            ? output.Replace("\\", "\\\\", StringComparison.Ordinal)
            : RelativeName(file).Replace("\\", "\\\\", StringComparison.Ordinal);

    internal string OutputName(int file) => _outputNames[file];

    // A file keeps its relative name where that is a safe path below the output directory;
    // otherwise it gets its bare name, and a numbered name when that is taken too.
    //
    // 相対名が出力ディレクトリの下の安全なパスならそのまま使い、そうでなければファイル名だけ、
    // それも使用済みなら番号付きの名前にする。
    private void AssignOutputNames(string mainOutputName)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mainOutputName };
        for (int file = MainFile + 1; file < _paths.Count; file++)
        {
            string relative = RelativeName(file);
            string name = IsSafe(relative) ? relative : FileName(relative);
            if (!IsSafe(name) || !taken.Add(name))
            {
                int number = 1;
                do
                {
                    name = $"include_{number++}.as";
                } while (!taken.Add(name));
            }

            _outputNames[file] = name;
        }
    }

    private static bool IsSafe(string relative)
    {
        string[] parts = relative.Split('\\', '/');
        return parts.All(part =>
            part.Length != 0
            && part != "."
            && part != ".."
            && !part.AsSpan().ContainsAny(s_badChars)
            && !part.Any(char.IsControl)
        );
    }

    private static string Directory(string path) => path[..(path.LastIndexOfAny(['\\', '/']) + 1)];

    private static string FileName(string path) => path[(path.LastIndexOfAny(['\\', '/']) + 1)..];
}
