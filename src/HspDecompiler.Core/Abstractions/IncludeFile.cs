using System.Collections.Generic;

namespace HspDecompiler.Core.Abstractions;

// A source file the decoder rebuilt beside the main source, under the name its #include uses.
//
// デコーダーがメインのソースの隣に復元したソースファイル。#include が使う名前を持つ。
internal sealed record IncludeFile(string Name, List<string> Lines);

// A decoder that rebuilds include files as well as the main source.
//
// メインのソースに加えて取り込みファイルも復元するデコーダー。
internal interface IIncludeSource
{
    IReadOnlyList<IncludeFile> Includes { get; }
}
