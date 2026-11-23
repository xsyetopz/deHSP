using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Containers;
using HspDecompiler.Core.Encoding;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Pipeline;

// Every file a run writes: decoded sources and their includes, extracted archive entries, the
// warning log, and the free names they go under. Nothing before this stage touches the disk
// for output.
//
// 1 回の実行が書くすべてのファイル。復元したソースと取り込みファイル、展開したアーカイブの
// エントリ、警告ログ、それらを置く空いた名前。この段より前は出力のためにディスクへ触れない。
internal sealed class OutputWriter(IDecompilerLogger logger)
{
    private static readonly CompositeFormat s_fileAlreadyExistsFormat = CompositeFormat.Parse(
        Strings.FileAlreadyExists
    );

    private static readonly CompositeFormat s_outputtingToFormat = CompositeFormat.Parse(
        Strings.OutputtingTo
    );

    private static readonly CompositeFormat s_lineNotEncodableFormat = CompositeFormat.Parse(
        Strings.LineNotEncodable
    );

    private static readonly CompositeFormat s_fileSaveFailedFormat = CompositeFormat.Parse(
        Strings.FileSaveFailed
    );

    private static readonly CompositeFormat s_directoryCreateFailedFormat = CompositeFormat.Parse(
        Strings.DirectoryCreateFailed
    );

    internal void WriteSource(
        DecodedSource source,
        string outputPath,
        System.Text.Encoding encoding
    )
    {
        logger.Write(Format(s_outputtingToFormat, Path.GetFileName(outputPath)));
        WarnUnencodableLines(source.Main, encoding);
        WriteLines(source.Main, outputPath, encoding);
        WriteIncludeFiles(source.Includes, Path.GetDirectoryName(outputPath) ?? ".", encoding);
        logger.Write(Strings.OutputComplete);
    }

    internal static void CreateDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception ex)
        {
            throw new HspDecoderException(Format(s_directoryCreateFailedFormat, directory), ex);
        }
    }

    // An extracted entry keeps its archive name, so an existing file of that name is left alone.
    //
    // 展開したエントリはアーカイブ内の名前のままにするため、同名のファイルが既にあれば残す。
    internal async Task SaveEntryAsync(ArchiveEntry entry, string directory, CancellationToken ct)
    {
        string path = Path.Combine(directory, entry.File.FileName ?? "");
        if (File.Exists(path))
        {
            logger.Write(Format(s_fileAlreadyExistsFormat, entry.File.FileName));
            return;
        }

        try
        {
            string? parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(entry.Bytes, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Warning(Format(s_fileSaveFailedFormat, entry.File.FileName) + " " + ex.Message);
        }
    }

    internal void WriteWarningLog(string path, DecompilerResult result)
    {
        if (logger.Warnings.Count == 0)
        {
            return;
        }

        using (var log = new StreamWriter(path, false, ShiftJisHelper.Encoding))
        {
            foreach (string warning in logger.Warnings)
            {
                log.WriteLine(warning);
            }
        }

        result.Warnings.AddRange(logger.Warnings);
    }

    internal static string FreeDirectoryName(string parentDir, string baseName) =>
        FreeName(parentDir, baseName, "", Directory.Exists);

    internal static string FreeFileName(string dir, string baseName, string extension) =>
        FreeName(dir, baseName, extension, File.Exists);

    // The name itself when nothing holds it, otherwise "name (1)", "name (2)" and so on.
    //
    // 何も使っていなければその名前、そうでなければ "名前 (1)"・"名前 (2)" のように番号を付ける。
    private static string FreeName(
        string dir,
        string baseName,
        string extension,
        Func<string, bool> taken
    )
    {
        string candidate = Path.Combine(dir, baseName + extension);
        for (int i = 1; taken(candidate); i++)
        {
            candidate = Path.Combine(dir, $"{baseName} ({i}){extension}");
        }

        return candidate;
    }

    // A string that UTF-8 AX data decoded to can hold characters Shift_JIS lacks, and the writer
    // would turn each into '?', so every such line gets a warning instead of a silent change.
    //
    // UTF-8 の AX データから復元した文字列には Shift_JIS に無い文字が入り得て、書き出し時に
    // それぞれ '?' に化けるため、黙って変えずに該当する行ごとに警告する。
    private void WarnUnencodableLines(List<string> lines, System.Text.Encoding encoding)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (
                !string.Equals(
                    encoding.GetString(encoding.GetBytes(line)),
                    line,
                    StringComparison.Ordinal
                )
            )
            {
                logger.Warning(Format(s_lineNotEncodableFormat, encoding.WebName), i + 1);
            }
        }
    }

    private static void WriteLines(List<string> lines, string path, System.Text.Encoding encoding)
    {
        using var writer = new StreamWriter(path, false, encoding);
        foreach (string line in lines)
        {
            writer.WriteLine(line);
        }
    }

    // An include keeps the exact name hspcmp recorded, so an existing file of that name is left
    // alone rather than renamed or overwritten.
    //
    // 取り込みファイルは hspcmp が記録した名前のままにする必要があるため、同名のファイルが既に
    // あれば名前を変えたり上書きしたりせずに残す。
    private void WriteIncludeFiles(
        IReadOnlyList<IncludeFile> includes,
        string directory,
        System.Text.Encoding encoding
    )
    {
        foreach (IncludeFile include in includes)
        {
            // A rebuilt HSP 3 include can sit in a subdirectory that hspcmp wrote with '\\'.
            //
            // 復元した HSP 3 の取り込みファイルは、hspcmp が '\\' で書いたサブディレクトリに
            // 置かれ得る。
            string path = Path.Combine(directory, include.Name.Replace('\\', '/'));
            if (File.Exists(path))
            {
                logger.Warning(Format(s_fileAlreadyExistsFormat, include.Name));
                continue;
            }

            logger.Write(Format(s_outputtingToFormat, include.Name));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            WriteLines(include.Lines, path, encoding);
        }
    }

    private static string Format(CompositeFormat format, object? argument) =>
        string.Format(CultureInfo.InvariantCulture, format, argument);
}
