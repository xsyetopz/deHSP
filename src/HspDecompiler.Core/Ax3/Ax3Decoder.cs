using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Ax3.Data.Analyzer;
using HspDecompiler.Core.Ax3.Data.Layout;
using HspDecompiler.Core.Ax3.Data.Line;
using HspDecompiler.Core.Ax3.Data.PP;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Pipeline;
using HspDecompiler.Core.Releases;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax3;

internal class Ax3Decoder : ISourceDecoder
{
    private Hsp3Dictionary? _dictionary;

    // The name the main source is written under; a rebuilt include must not take it.
    //
    // メインのソースを書き出す名前。復元した取り込みファイルはこの名前を使えない。
    internal string MainFileName { get; init; } = "start.hsp";

    // The directory of the caller's common files (hspcmp --compath), used to restore the
    // #include of a common header that left no code.
    //
    // 呼び出し側の共通ファイルのディレクトリ(hspcmp の --compath)。コードを残さなかった
    // 共通ヘッダーの #include を復元するのに使う。
    internal string? CommonDirectory { get; init; }

    internal SourceStyle Style { get; init; } = SourceStyle.Default;

    internal Hsp3Dictionary? Dictionary
    {
        get => _dictionary;
        set => _dictionary = value;
    }

    public async Task<List<string>> DecodeAsync(
        BinaryReader reader,
        IDecompilerLogger logger,
        IProgressReporter progress,
        CancellationToken ct = default
    ) => (await DecodeSourceAsync(reader, logger, progress, ct).ConfigureAwait(false)).Main;

    public async Task<DecodedSource> DecodeSourceAsync(
        BinaryReader reader,
        IDecompilerLogger logger,
        IProgressReporter progress,
        CancellationToken ct
    )
    {
        var data = new AxData { Style = Style };
        try
        {
            logger.Write(Strings.AnalyzingHeader);
            data.LoadStart(reader, _dictionary);
            data.ReadHeader();
            logger.Write(Strings.AnalyzingPreprocessor);
            data.ReadPreprocessor(_dictionary);
            logger.Write(Strings.LexicalAnalysis);
            var lex = new LexicalAnalyzer(_dictionary!);
            TokenCollection stream = lex.Analyze(data);
            data.Release = ReleaseProfile.Infer(
                new AxEvidence(
                    data.Header!.Version,
                    data.DebugInfo.HasLines ? 0 : data.DebugInfo.Marks.Count,
                    data.HasSingleItemFlags,
                    FramesOf(data)
                )
            );
            data.ScriptStrings.Resolve(
                data.Runtime != null,
                BootOptionDirectives.IsUtf8Runtime(data.Header!.BootOption)
            );
            IncludeNames? names = data.DebugInfo.HasLines ? IncludeNamesOf(data) : null;
            HashSet<int> restored =
                names == null ? [] : CommonIncludes.Attach(data, names, CommonDirectory);
            DebugNames.ApplyParameterNames(data);
            data.LoadEnd();
            logger.Write(Strings.SyntacticAnalysis);
            var synt = new SyntacticAnalyzer();
            List<LogicalLine> lines = await synt.AnalyzeAsync(stream, data, logger, progress, ct)
                .ConfigureAwait(false);
            logger.Write(Strings.CreatingOutputFile);
            DecodedSource source = Write(lines, data, names, restored);
            WarnStructs(source, logger);
            return source;
        }
        catch (SystemException e)
        {
            throw new HspDecoderException("AxData", Strings.UnexpectedError, e);
        }
    }

    // A debug build places each line by its record. A normal build from hspcmp 3.1 to 3.32
    // records only its #module, #global and {"..."} lines, which MarkedLayout rebuilds when they
    // fit; other builds write the lines in order.
    //
    // デバッグビルドは各行を記録に従って置く。hspcmp 3.1〜3.32 の通常ビルドは #module・
    // #global・{"..."} の行だけを記録し、収まれば MarkedLayout がそれを組み直す。それ以外の
    // ビルドは行を順に書く。
    private DecodedSource Write(
        List<LogicalLine> lines,
        AxData data,
        IncludeNames? names,
        HashSet<int> restored
    )
    {
        if (names != null)
        {
            bool unused = lines.Exists(line => line is ModuleStart { Used: false });
            return SourceLayout.Build(
                lines,
                CompileDirectives(data, unused),
                data,
                names,
                restored
            );
        }

        return Marked(lines, data)
            ?? DecodedSource.Single([.. CompileDirectives(data, false), .. Indented(lines, data)]);
    }

    private DecodedSource? Marked(List<LogicalLine> lines, AxData data) =>
        data.DebugInfo.Marks.Count > 0
        && data.Release.MayWriteNormalBuildRecords
        && MarkedLayout.Match(lines, data, DirectivePlanner.HasUncalledImport(data)) is { } marked
            ? marked.Write(CompileDirectives(data, marked.HasUnusedModule), MainFileName)
            : null;

    // The warning names the written line, which exists only once a layout has placed every
    // line, so the scan reads the output. A rebuilt #module writes the members itself and leaves
    // no #struct line behind.
    //
    // 警告は書き出した行の番号を示す。その番号は配置がすべての行を置いた後にしか無いため、
    // 出力を読む。組み直した #module はメンバーを自分で書き、#struct 行を残さない。
    private static void WarnStructs(DecodedSource source, IDecompilerLogger logger)
    {
        foreach (
            List<string> file in source
                .Includes.Select(include => include.Lines)
                .Prepend(source.Main)
        )
        {
            for (int i = 0; i < file.Count; i++)
            {
                if (file[i].StartsWith(Function.StructDirective, StringComparison.Ordinal))
                {
                    logger.Warning(Strings.StructNotInHspSpec, i + 1);
                }
            }
        }
    }

    // The parameter layout the first user function that tells the two apart shows.
    //
    // 2 つの並べ方を区別できる最初のユーザー関数が示す引数の並べ方。
    private static FrameLayout? FramesOf(AxData data) =>
        data
            .Functions.Where(f => f.IsUserFunction)
            .Select(f => FrameLayouts.Of(f.Parameters.Select(p => p.TypeCode), f.FrameSize))
            .FirstOrDefault(layout => layout != null);

    private static IEnumerable<string> Indented(List<LogicalLine> lines, AxData data) =>
        lines
            .Where(line => line.Visible)
            .Select(line => new string('\t', line.TabCount) + data.Scope.Render(line));

    private IncludeNames IncludeNamesOf(AxData data) =>
        new([.. data.DebugInfo.FileNames.Select(data.DecodeName)], MainFileName);

    private static List<string> CompileDirectives(AxData data, bool unusedModule) =>
        DirectivePlanner.Plan(DirectivePlanner.FactsOf(data, unusedModule));
}
