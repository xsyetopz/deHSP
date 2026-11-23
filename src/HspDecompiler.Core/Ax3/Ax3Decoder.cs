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
using HspDecompiler.Core.Ax3.Data.PP;
using HspDecompiler.Core.Ax3.Naming;
using HspDecompiler.Core.Ax3.Reading;
using HspDecompiler.Core.Containers;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Pipeline;
using HspDecompiler.Core.Releases;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax3;

internal sealed class Ax3Decoder(DecodeSettings settings) : ISourceDecoder
{
    public async Task<DecodedSource> DecodeAsync(
        AxImage image,
        string mainFileName,
        IDecompilerLogger logger,
        IProgressReporter progress,
        CancellationToken ct
    )
    {
        using BinaryReader reader = image.Open();
        var data = new AxData { Dictionary = settings.Dictionary, Style = settings.Style };
        try
        {
            logger.Write(Strings.AnalyzingHeader);
            var axImage = new Ax3ImageReader(reader, data.ScriptStrings);
            data.Header = axImage.Header;
            logger.Write(Strings.AnalyzingPreprocessor);
            axImage.ReadTables(data);
            NameAssigner.NameFunctions(data.Functions, settings.Dictionary);
            data.DebugInfo = axImage.ReadDebugInfo();
            logger.Write(Strings.LexicalAnalysis);
            var lex = new LexicalAnalyzer(settings.Dictionary);
            TokenCollection stream = lex.Analyze(axImage, data);
            data.HasRepeatedStrings = axImage.HasRepeatedStrings;
            data.ScriptStrings.Resolve(
                data.Runtime != null,
                BootOptionDirectives.IsUtf8Runtime(data.Header.BootOption)
            );
            AxEvidence evidence = EvidenceReader.Read(data);
            data.Release = settings.ReleaseFor(evidence, logger);
            IncludeNames? names = data.DebugInfo.HasLines
                ? IncludeNamesOf(data, mainFileName)
                : null;
            HashSet<int> restored =
                names == null ? [] : CommonIncludes.Attach(data, names, settings.CommonFiles);
            DebugNames.ApplyParameterNames(data);
            logger.Write(Strings.SyntacticAnalysis);
            List<LogicalLine> lines = await SyntacticAnalyzer
                .AnalyzeAsync(stream, data, logger, progress, ct)
                .ConfigureAwait(false);
            logger.Write(Strings.CreatingOutputFile);
            DecodedSource source = Write(lines, data, evidence, names, restored, mainFileName);
            WarnStructs(source, logger);
            return source;
        }
        catch (SystemException e)
        {
            throw new HspDecoderException("AxData", Strings.UnexpectedError, e);
        }
    }

    private static DecodedSource Write(
        List<LogicalLine> lines,
        AxData data,
        AxEvidence evidence,
        IncludeNames? names,
        HashSet<int> restored,
        string mainFileName
    ) =>
        LayoutStrategies.Layout(
            new LayoutInput(lines, data, evidence, names, restored, mainFileName, unused =>
                DirectivePlanner.Plan(DirectivePlanner.FactsOf(data, evidence, unused))
            )
        );

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

    private static IncludeNames IncludeNamesOf(AxData data, string mainFileName) =>
        new([.. data.DebugInfo.FileNames.Select(data.DecodeName)], mainFileName);
}
