using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HspDecompiler.Core.Abstractions;

// A source file the decoder rebuilt beside the main source, under the name its #include uses.
//
// デコーダーがメインのソースの隣に復元したソースファイル。#include が使う名前を持つ。
internal sealed record IncludeFile(string Name, List<string> Lines);

// The main source and the include files one AX decodes to. Every decoder and layout returns
// this one shape, so the pipeline writes AX2 and AX3 output the same way.
//
// 1 つの AX から復元したメインのソースと取り込みファイル。すべてのデコーダーとレイアウトが
// この 1 つの形を返すため、パイプラインは AX2 と AX3 の出力を同じ方法で書き出す。
internal sealed record DecodedSource(List<string> Main, IReadOnlyList<IncludeFile> Includes)
{
    internal static DecodedSource Single(List<string> main) => new(main, []);
}

// A decoder that returns the whole decoded source, include files with it.
//
// 取り込みファイルを含め、復元したソース全体を返すデコーダー。
internal interface ISourceDecoder : IAxDecoder
{
    Task<DecodedSource> DecodeSourceAsync(
        BinaryReader reader,
        IDecompilerLogger logger,
        IProgressReporter progress,
        CancellationToken ct
    );
}
