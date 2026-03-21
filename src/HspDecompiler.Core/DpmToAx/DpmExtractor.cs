using System;
using System.Collections.Generic;
using System.IO;
using HspDecompiler.Core.ExeToDpm;

namespace HspDecompiler.Core.DpmToAx;

internal sealed class DpmExtractor
{
    private DpmExtractor()
    {
    }

    internal static DpmExtractor? FromBinaryReader(BinaryReader reader)
    {
        var ret = new DpmExtractor();
        try
        {
            ret._reader = reader;
            if (ret.ReadHeader())
            {
                return ret;
            }
        }
        catch (IOException)
        {
            return null;
        }
        return null;
    }

    private long _startPosition;
    private long _streamLength;
    private long _fileOffsetStart;

    private bool ReadHeader()
    {
        _startPosition = _reader!.BaseStream.Position;
        _streamLength = _reader.BaseStream.Length - _startPosition;
        char[] identifier = _reader.ReadChars(4);
        if (identifier.Length < 4)
        {
            return false;
        }

        _reader.BaseStream.Seek(_startPosition, SeekOrigin.Begin);
        if ((identifier[0] == 'M') && (identifier[1] == 'Z'))
        {
            var winHeader = Win32PeHeader.FromBinaryReader(_reader);
            if (winHeader == null)
            {
                return false;
            }
            _startPosition += winHeader.EndOfExecutableRegion;
            _streamLength = _reader.BaseStream.Length - _startPosition;
            _reader.BaseStream.Seek(_startPosition, SeekOrigin.Begin);
            identifier = _reader.ReadChars(4);
            if (identifier.Length < 4)
            {
                return false;
            }
        }
        if (!((identifier[0] == 'D') && (identifier[1] == 'P') && (identifier[2] == 'M') && (identifier[3] == 'X')))
        {
            return false;
        }
        _reader.BaseStream.Seek(_startPosition, SeekOrigin.Begin);
        _reader.ReadInt32();
        _reader.ReadInt32();
        int fileCount = _reader.ReadInt32();
        _reader.ReadInt32();
        _files.Capacity = fileCount;
        _fileOffsetStart = _startPosition + 0x10 + fileCount * 0x20;
        for (int i = 0; i < fileCount; i++)
        {
            var file = new DpmFileEntry();
            char[] chars = _reader.ReadChars(16);
            int stringLength = 16;
            for (int j = 0; j < 16; j++)
            {
                if (chars[j] == '\0')
                {
                    stringLength = j;
                    break;
                }
            }
            file.FileName = new string(chars, 0, stringLength);
            file.Unknown = _reader.ReadInt32();
            file.EncryptionKey = _reader.ReadInt32();
            file.FileOffset = _reader.ReadInt32();
            file.FileSize = _reader.ReadInt32();
            if ((file.FileOffset + file.FileSize) > (_streamLength))
            {
                return false;
            }

            _files.Add(file);
        }

        return true;
    }

    private BinaryReader? _reader;
    private readonly List<DpmFileEntry> _files = new();

    internal List<DpmFileEntry> FileList => _files;

    internal byte[] GetFile(int fileOffset, int fileSize)
    {
        _reader!.BaseStream.Seek(fileOffset, SeekOrigin.Begin);
        byte[] buffer = new byte[fileSize];
        _reader.BaseStream.ReadExactly(buffer, 0, fileSize);
        return buffer;
    }

    /// <summary>
    /// Returns the "start.ax" entry, or null if not present.
    /// Consolidated from the former GetStartAx/SeekStartAx pair.
    /// </summary>
    internal DpmFileEntry? GetStartAx()
    {
        foreach (DpmFileEntry file in _files)
        {
            if (file.FileName != null && file.FileName.Equals("start.ax", StringComparison.Ordinal))
            {
                return file;
            }
        }
        return null;
    }

    internal bool Seek(DpmFileEntry file)
    {
        try
        {
            _reader!.BaseStream.Seek(file.FileOffset + _fileOffsetStart, SeekOrigin.Begin);
        }
        catch (IOException)
        {
            return false;
        }
        return true;
    }
}
