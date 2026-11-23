using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Containers;

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

// Decodes one AX image of the format it is built for into the main source and its include
// files. mainFileName is the name the main source is written under, which a rebuilt include
// must not take.
//
// 自身が対象とする形式の AX イメージを、メインのソースと取り込みファイルへ復元する。
// mainFileName はメインのソースを書き出す名前で、復元した取り込みファイルはこの名前を使えない。
internal interface ISourceDecoder
{
    Task<DecodedSource> DecodeAsync(
        AxImage image,
        string mainFileName,
        IDecompilerLogger logger,
        IProgressReporter progress,
        CancellationToken ct
    );
}
