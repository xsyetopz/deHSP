using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Abstractions;
using HspDecompiler.Core.Ax2;
using HspDecompiler.Core.Ax3;
using HspDecompiler.Core.DpmToAx;
using HspDecompiler.Core.DpmToAx.Crypto;
using HspDecompiler.Core.Encoding;
using HspDecompiler.Core.Exceptions;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.Pipeline
{
    public sealed class DecompilerPipeline
    {
        private readonly IDecompilerLogger _logger;
        private readonly IProgressReporter _progress;
        private Hsp3Dictionary? _dictionary;

        public DecompilerPipeline(IDecompilerLogger logger, IProgressReporter progress)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(progress);
            _logger = logger;
            _progress = progress;
        }

        public bool Initialize(string dictionaryPath)
        {
            try
            {
                _dictionary = Hsp3Dictionary.FromFile(dictionaryPath);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex.Message);
                _dictionary = null;
            }

            if (_dictionary != null)
            {
                _logger.Write(string.Format(Strings.DictionaryLoadSuccess, Path.GetFileName(dictionaryPath)));
                return true;
            }

            _logger.Write(string.Format(Strings.DictionaryLoadFailed, Path.GetFileName(dictionaryPath)));
            return false;
        }

        public async Task<DecompilerResult> RunAsync(DecompilerOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            var decompilerOutput = new DecompilerResult();
            string errorLogPath = options.InputPath + ".log";

            try
            {
                _logger.Write(string.Format(Strings.ReadingFile, Path.GetFileName(options.InputPath)));
                using var stream = new FileStream(options.InputPath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(stream, ShiftJisHelper.Encoding);

                string magic = DetectFileFormat(reader);

                string inputDir = (Path.GetDirectoryName(options.InputPath) ?? ".") + Path.DirectorySeparatorChar;
                string inputBaseName = Path.GetFileNameWithoutExtension(options.InputPath);

                if (magic.StartsWith("MZ", StringComparison.Ordinal) || magic.StartsWith("DPM", StringComparison.Ordinal))
                {
                    string outputDir = BuildAutoIncrementDirName(inputDir, inputBaseName);
                    errorLogPath = outputDir.TrimEnd(Path.DirectorySeparatorChar) + ".log";
                    outputDir = outputDir + Path.DirectorySeparatorChar;

                    var dpmResult = await DecompressDpmAsync(reader, outputDir, options, ct);
                    decompilerOutput.DpmFiles.AddRange(dpmResult.Files);
                    decompilerOutput.OutputPath = outputDir;

                    if (dpmResult.AllEncrypted || dpmResult.Cancelled)
                    {
                        decompilerOutput.Success = !dpmResult.AllEncrypted;
                        if (dpmResult.AllEncrypted)
                        {
                            decompilerOutput.ErrorMessage = Strings.AllFilesEncrypted;
                        }

                        return decompilerOutput;
                    }

                    foreach (var entry in dpmResult.Files)
                    {
                        if (entry.IsEncrypted)
                        {
                            continue;
                        }

                        string axPath = Path.Combine(outputDir, entry.FileName ?? "");
                        if (!File.Exists(axPath))
                        {
                            continue;
                        }

                        if (entry.FileName == null || !entry.FileName.EndsWith(".ax", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        using var axStream = new FileStream(axPath, FileMode.Open, FileAccess.Read);
                        using var axReader = new BinaryReader(axStream, ShiftJisHelper.Encoding);
                        char[] axMagicChars = axReader.ReadChars(4);
                        string axMagic = new string(axMagicChars);
                        axReader.BaseStream.Seek(0, SeekOrigin.Begin);

                        string ext = axMagic.StartsWith("HSP2", StringComparison.Ordinal) ? ".as" : ".hsp";
                        string axBaseName = Path.GetFileNameWithoutExtension(entry.FileName);
                        string outputPath = BuildAutoIncrementFileName(outputDir, axBaseName, ext);

                        await DecodeAsync(axReader, outputPath, ct);
                    }
                }
                else if (magic.StartsWith("HSP2", StringComparison.Ordinal) || magic.StartsWith("HSP3", StringComparison.Ordinal))
                {
                    string ext = magic.StartsWith("HSP2", StringComparison.Ordinal) ? ".as" : ".hsp";
                    string outputPath = BuildAutoIncrementFileName(inputDir, inputBaseName, ext);
                    errorLogPath = outputPath + ".log";
                    decompilerOutput.OutputPath = outputPath;

                    await DecodeAsync(reader, outputPath, ct);
                }
                else
                {
                    throw new HspDecoderException(Strings.UnrecognizedFileFormat);
                }

                if (_logger.Warnings.Count > 0)
                {
                    using (var errorLog = new StreamWriter(errorLogPath, false, ShiftJisHelper.Encoding))
                    {
                        foreach (string warning in _logger.Warnings)
                        {
                            errorLog.WriteLine(warning);
                        }
                    }
                    decompilerOutput.Warnings.AddRange(_logger.Warnings);
                }

                decompilerOutput.Success = true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                decompilerOutput.Success = false;
                decompilerOutput.ErrorMessage = ex.Message;
            }

            return decompilerOutput;
        }

        private string DetectFileFormat(BinaryReader reader)
        {
            char[] buffer = reader.ReadChars(4);
            string magic = new string(buffer);
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            return magic;
        }

        public async Task<DpmExtractionResult> DecompressDpmAsync(BinaryReader reader, string outputDir, DecompilerOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(reader);

            var dpmResult = new DpmExtractionResult();

            _logger.Write(Strings.SearchingDpmHeader);
            DpmExtractor? extractor = DpmExtractor.FromBinaryReader(reader);
            if (extractor == null)
            {
                throw new HspDecoderException(Strings.DpmHeaderNotFound);
            }

            if (extractor.FileList == null || extractor.FileList.Count == 0)
            {
                throw new HspDecoderException(Strings.DpmNoFiles);
            }

            int encryptedCount = 0;
            foreach (DpmFileEntry file in extractor.FileList)
            {
                dpmResult.Files.Add(file);
                if (file.IsEncrypted)
                {
                    encryptedCount++;
                }
            }

            dpmResult.EncryptedCount = encryptedCount;

            int totalCount = extractor.FileList.Count;
            if (totalCount - encryptedCount <= 0)
            {
                dpmResult.AllEncrypted = true;
                _logger.Write(Strings.ExtractionCancelled);
                return dpmResult;
            }

            if (encryptedCount > 0 && !options.SkipEncrypted && !options.AllowDecryption)
            {
                dpmResult.Cancelled = true;
                _logger.Write(Strings.ExtractionCancelled);
                return dpmResult;
            }

            if (!Directory.Exists(outputDir))
            {
                try
                {
                    Directory.CreateDirectory(outputDir);
                }
                catch (Exception ex)
                {
                    throw new HspDecoderException(string.Format(Strings.DirectoryCreateFailed, outputDir), ex);
                }
            }

            foreach (DpmFileEntry file in extractor.FileList)
            {
                await WriteDpmEntryAsync(reader, extractor, file, outputDir, options, ct);
            }

            _logger.Write(Strings.ExtractionComplete);
            return dpmResult;
        }

        private async Task WriteDpmEntryAsync(
            BinaryReader reader,
            DpmExtractor extractor,
            DpmFileEntry file,
            string outputDir,
            DecompilerOptions options,
            CancellationToken ct)
        {
            if (file.IsEncrypted && !options.AllowDecryption)
            {
                _logger.Write(string.Format(Strings.FileEncrypted, file.FileName));
                return;
            }

            string outputPath = Path.Combine(outputDir, file.FileName ?? "");
            if (File.Exists(outputPath))
            {
                _logger.Write(string.Format(Strings.FileAlreadyExists, file.FileName));
                return;
            }

            if (!extractor.Seek(file))
            {
                _logger.Write(string.Format(Strings.FileSeekFailed, file.FileName));
                return;
            }

            byte[] fileData = reader.ReadBytes(file.FileSize);

            if (file.IsEncrypted)
            {
                await CrackDpmEncryptionAsync(fileData, file, outputDir, outputPath, ct);
            }
            else
            {
                try
                {
                    using var saveStream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write);
                    saveStream.Write(fileData, 0, fileData.Length);
                }
                catch
                {
                    _logger.Warning(string.Format(Strings.FileSaveFailed, file.FileName));
                }
            }
        }

        private async Task CrackDpmEncryptionAsync(
            byte[] fileData,
            DpmFileEntry file,
            string outputDir,
            string outputPath,
            CancellationToken ct)
        {
            _logger.Write(string.Format(Strings.DecryptingFile, file.FileName));

            string decryptedBaseName = Path.GetFileNameWithoutExtension(file.FileName) ?? file.FileName ?? "";
            string resolvedOutputPath = BuildAutoIncrementFileName(outputDir, decryptedBaseName, ".hsp");
            bool decryptionSucceeded = false;

            Func<byte[], bool> validator = (decryptedData) =>
            {
                try
                {
                    using var ms = new MemoryStream(decryptedData);
                    using var br = new BinaryReader(ms, ShiftJisHelper.Encoding);
                    char[] magicChars = br.ReadChars(4);
                    string fileMagic = new string(magicChars);
                    br.BaseStream.Seek(0, SeekOrigin.Begin);

                    if (!fileMagic.StartsWith("HSP2", StringComparison.Ordinal) &&
                        !fileMagic.StartsWith("HSP3", StringComparison.Ordinal))
                    {
                        return false;
                    }

                    string resolvedExt = fileMagic.StartsWith("HSP2", StringComparison.Ordinal) ? ".as" : ".hsp";
                    string candidatePath = BuildAutoIncrementFileName(outputDir, decryptedBaseName, resolvedExt);

                    IAxDecoder decoder = CreateDecoder(br);
                    // CrackEncryption is synchronous; validator must be synchronous too.
                    // Blocking here is unavoidable given the HspCryptoTransform.CrackEncryption API contract.
                    List<string> lines = decoder.DecodeAsync(br, _logger, _progress).GetAwaiter().GetResult();
                    WriteOutputLines(lines, candidatePath);
                    resolvedOutputPath = candidatePath;
                    decryptionSucceeded = true;
                    return true;
                }
                catch
                {
                    return false;
                }
            };

            HspCryptoTransform? decryptor = HspCryptoTransform.CrackEncryption(fileData, validator);
            if (decryptor == null || !decryptionSucceeded)
            {
                _logger.Write(string.Format(Strings.DecryptionFailed, file.FileName));
                return;
            }

            byte[] decryptedBytes = decryptor.Decryption(fileData);
            try
            {
                using var saveStream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write);
                saveStream.Write(decryptedBytes, 0, decryptedBytes.Length);
            }
            catch
            {
                _logger.Warning(string.Format(Strings.FileSaveFailed, file.FileName));
            }

            await Task.CompletedTask;
        }

        public async Task DecodeAsync(BinaryReader reader, string outputPath, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(reader);

            if (_dictionary == null)
            {
                throw new InvalidOperationException(Strings.DictionaryNotInitialized);
            }

            _logger.StartSection();
            _logger.Write(Strings.Decompiling);
            _logger.StartSection();

            IAxDecoder decoder = CreateDecoder(reader);
            List<string> lines = await decoder.DecodeAsync(reader, _logger, _progress, ct);

            _logger.EndSection();
            _logger.Write(Strings.DecompileComplete);
            _logger.EndSection();
            _logger.Write(string.Format(Strings.OutputtingTo, Path.GetFileName(outputPath)));

            WriteOutputLines(lines, outputPath);
            _logger.Write(Strings.OutputComplete);
        }

        private IAxDecoder CreateDecoder(BinaryReader reader)
        {
            long startPosition = reader.BaseStream.Position;
            char[] buffer = reader.ReadChars(4);
            string magic = new string(buffer);
            reader.BaseStream.Seek(startPosition, SeekOrigin.Begin);

            if (magic.Equals("HSP2", StringComparison.Ordinal))
            {
                return new Ax2Decoder();
            }

            if (magic.Equals("HSP3", StringComparison.Ordinal))
            {
                var decoder = new Ax3Decoder();
                decoder.Dictionary = _dictionary;
                return decoder;
            }

            throw new HspDecoderException(Strings.NotHsp2OrHsp3);
        }

        private static void WriteOutputLines(List<string> lines, string outputPath)
        {
            using var writer = new StreamWriter(outputPath, false, ShiftJisHelper.Encoding);
            foreach (string line in lines)
            {
                writer.WriteLine(line);
            }
        }

        private static string BuildAutoIncrementDirName(string parentDir, string baseName)
        {
            string candidate = Path.Combine(parentDir, baseName);
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }

            int i = 1;
            string directoryPath;
            do
            {
                directoryPath = Path.Combine(parentDir, $"{baseName} ({i})");
                i++;
            }
            while (Directory.Exists(directoryPath));

            return directoryPath;
        }

        private static string BuildAutoIncrementFileName(string dir, string baseName, string extension)
        {
            string candidate = Path.Combine(dir, baseName + extension);
            if (!File.Exists(candidate))
            {
                return candidate;
            }

            int i = 1;
            string filePath;
            do
            {
                filePath = Path.Combine(dir, $"{baseName} ({i}){extension}");
                i++;
            }
            while (File.Exists(filePath));

            return filePath;
        }
    }
}
