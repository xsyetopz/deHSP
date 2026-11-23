using System;
using System.Collections.Generic;
using System.Linq;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax3.Data.Line;
using HspDecompiler.Core.Releases;

namespace HspDecompiler.Core.Ax3.Data.Layout;

// What a layout reads: the decoded lines, the data and evidence they came from, the include
// names a debug build records, and the directives the source opens with, which depend on
// whether the layout keeps a module no code uses.
//
// 配置が読むもの。デコードした行、その元のデータと証拠、デバッグビルドが記録する取り込み
// ファイル名、そしてソースの先頭の指示文。指示文は、どのコードも使わないモジュールを配置が
// 残すかどうかで変わる。
internal sealed record LayoutInput(
    List<LogicalLine> Lines,
    AxData Data,
    AxEvidence Evidence,
    IncludeNames? Names,
    ISet<int> RestoredIncludes,
    string MainFileName,
    Func<bool, List<string>> Directives
);

// One way to place the decoded lines in source files. Applies says whether the AX carries what
// the layout needs; TryLayout returns null when the lines do not fit, and leaves the data as it
// found it then, so the next layout starts from the same state.
//
// デコードした行をソースファイルに置く方法の 1 つ。Applies は配置が要るものを AX が持つかを
// 示す。TryLayout は行が収まらなければ null を返し、その時はデータを元のままに残す。次の配置が
// 同じ状態から始められるようにするためである。
internal interface ILayoutStrategy
{
    bool Applies(LayoutInput input);

    DecodedSource? TryLayout(LayoutInput input);
}

internal static class LayoutStrategies
{
    // The first layout that applies and fits writes the source. A debug build places every line
    // by its record; a normal build whose release may write records rebuilds the recorded lines;
    // any other build writes the lines in order, which always fits.
    //
    // 適用でき収まる最初の配置がソースを書く。デバッグビルドは各行を記録に従って置き、記録を
    // 書きうるリリースの通常ビルドは記録された行を組み直す。それ以外は行を順に書き、これは常に
    // 収まる。
    private static readonly ILayoutStrategy[] s_order =
    [
        new DebugLayout(),
        new MarkedLayoutStrategy(),
        new InOrderLayout(),
    ];

    internal static DecodedSource Layout(LayoutInput input) =>
        s_order
            .Where(layout => layout.Applies(input))
            .Select(layout => layout.TryLayout(input))
            .First(source => source != null)!;
}

internal sealed class DebugLayout : ILayoutStrategy
{
    public bool Applies(LayoutInput input) => input.Names != null;

    public DecodedSource? TryLayout(LayoutInput input)
    {
        bool unused = input.Lines.Exists(line => line is ModuleStart { Used: false });
        return SourceLayout.Build(
            input.Lines,
            input.Directives(unused),
            input.Data,
            input.Names!,
            input.RestoredIncludes
        );
    }
}

// hspcmp 3.1 to 3.32 record the #module, #global and {"..."} lines of a normal build too.
//
// hspcmp 3.1〜3.32 は通常ビルドでも #module・#global・{"..."} の行を記録する。
internal sealed class MarkedLayoutStrategy : ILayoutStrategy
{
    public bool Applies(LayoutInput input) =>
        input.Data.DebugInfo.Marks.Count > 0 && input.Data.Release.MayWriteNormalBuildRecords;

    public DecodedSource? TryLayout(LayoutInput input) =>
        MarkedLayout.Match(input.Lines, input.Data, input.Evidence.UncalledImport) is { } marked
            ? marked.Write(input.Directives(marked.HasUnusedModule), input.MainFileName)
            : null;
}

internal sealed class InOrderLayout : ILayoutStrategy
{
    public bool Applies(LayoutInput input) => true;

    public DecodedSource? TryLayout(LayoutInput input) =>
        DecodedSource.Single([
            .. input.Directives(false),
            .. input
                .Lines.Where(line => line.Visible)
                .Select(line => new string('\t', line.TabCount) + input.Data.Scope.Render(line)),
        ]);
}
