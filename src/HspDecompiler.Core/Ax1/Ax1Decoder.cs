using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax1.Data;
using HspDecompiler.Core.Ax1.Reading;
using HspDecompiler.Core.Ax1.Writing;
using HspDecompiler.Core.Containers;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Pipeline;
using HspDecompiler.Core.Releases;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax1;

// Decodes the "Aspo" images HSP 1's ASC.EXE writes. The layout comes from ASC.EXE 1.1 and 1.2f
// read with Ghidra and from probe sources compiled with both, in debug and /r builds.
//
// HSP 1 の ASC.EXE が書く "Aspo" イメージを復元する。形式は Ghidra で読んだ ASC.EXE 1.1 と
// 1.2f、および両方でデバッグと /r でコンパイルした試験用のソースに基づく。
internal sealed class Ax1Decoder(DecodeSettings settings) : ISourceDecoder
{
    // 1.1 numbers the first user variable 0x10 and 1.2f, which added two system variables,
    // 0x20; an index between 0x12 and 0x1f, or a count below 0x20, is 1.1's.
    //
    // 1.1 は最初のユーザー変数を 0x10、システム変数を 2 つ加えた 1.2f は 0x20 とする。0x12 から
    // 0x1f の番号、または 0x20 未満の数は 1.1 のものである。
    private const int Wide = 0x20;
    private const int Narrow = 0x10;

    private static readonly CompositeFormat s_differsFormat = CompositeFormat.Parse(
        Strings.Ax1Differs
    );

    public Task<DecodedSource> DecodeAsync(
        AxImage image,
        string mainFileName,
        IDecompilerLogger logger,
        IProgressReporter progress,
        CancellationToken ct
    )
    {
        try
        {
            logger.Write(Strings.AnalyzingHeader);
            Ax1Program program = Ax1Reader.Read(image.Bytes.ToArray());
            int first = FirstVariable(program);
            ReleaseProfile release = settings.ReleaseFor(
                new AxEvidence(CompilerReleases.Word1, 0, false) { FirstVariable = first },
                logger
            );
            logger.Write(Strings.AnalyzingData);
            var symbols = new Ax1Symbols(first, program.NextGlobal, i =>
                settings.Dictionary.ExactNameOf(0x13, i, release)
            );
            var context = new Ax1Context(
                program,
                settings.Dictionary,
                release,
                symbols,
                (offset, reason) =>
                    logger.Warning(
                        string.Format(CultureInfo.InvariantCulture, s_differsFormat, offset, reason)
                    )
            );
            WarnLabels(program, logger);
            System.Collections.Generic.List<string> lines = Ax1Layout.Write(context);
            if (symbols.NextGlobal != program.NextGlobal || symbols.NextLocal != program.NextLocal)
            {
                context.Differs(0, Strings.Ax1VariableCount);
            }

            return Task.FromResult(DecodedSource.Single(lines));
        }
        catch (System.SystemException e)
        {
            throw new HspDecoderException("AxData", Strings.UnexpectedError, e);
        }
    }

    private static int FirstVariable(Ax1Program program) =>
        program.NextGlobal < Wide
        || program.Statements.Any(s =>
            s.Tokens.Any(t =>
                t.Kind == Ax1TokenKind.Variable && t.Value is >= Narrow + 2 and < Wide
            )
        )
            ? Narrow
            : Wide;

    private static void WarnLabels(Ax1Program program, IDecompilerLogger logger)
    {
        bool indicesAfterEnd = program.Statements.Any(s =>
            s.Tokens.Any(t => t.Kind == Ax1TokenKind.LabelIndex && t.Offset > program.FirstEnd)
        );
        if (program.Unresolved)
        {
            logger.Warning(Strings.Ax1UnresolvedLabels);
        }
        else if (indicesAfterEnd)
        {
            logger.Warning(Strings.Ax1LabelsAfterEnd);
        }
    }
}
