using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax2.Data;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Ax2;

internal sealed class Ax2Decoder : IAxDecoder
{
    internal string? MainFileName { get; init; }

    internal IReadOnlyList<IncludeFile> Includes { get; private set; } = [];

    public Task<List<string>> DecodeAsync(
        BinaryReader reader,
        IDecompilerLogger logger,
        IProgressReporter progress,
        CancellationToken ct = default
    )
    {
        AxData data;
        try
        {
            logger.Write(Strings.AnalyzingHeader);
            data = AxData.FromStream(reader.BaseStream);

            logger.Write(Strings.AnalyzingData);
            data.Decompile();
        }
        catch (System.SystemException e)
        {
            throw new HspDecoderException("AxData", Strings.UnexpectedError, e);
        }

        Ax2Source source = data.BuildSource(MainFileName);
        Includes = source.Includes;
        return Task.FromResult(source.Lines);
    }
}
