using System;
using System.Collections.Generic;
using System.IO;
using HspDecompiler.Core.ExeToDpm;

namespace HspDecompiler.Core.DpmToAx;

internal sealed class DpmExtractor
{
    private DpmExtractor() { }

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
    private bool _isDpm2;
    private int _crcSeed;
    private int _salt;

    internal bool IsDpm2 => _isDpm2;
    internal int CrcSeed => _crcSeed;
    internal int Salt => _salt;

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
        if (!((identifier[0] == 'D') && (identifier[1] == 'P') && (identifier[2] == 'M')))
        {
            return false;
        }
        bool isDpm2;
        if (identifier[3] == 'X')
        {
            isDpm2 = false;
        }
        else if (identifier[3] == '2')
        {
            isDpm2 = true;
        }
        else
        {
            return false;
        }
        _reader.BaseStream.Seek(_startPosition, SeekOrigin.Begin);
        return isDpm2 ? ReadDpm2Header() : ReadDpmxHeader();
    }

    private BinaryReader? _reader;
    private readonly List<DpmFileEntry> _files = [];

    // DPMX layout: a 16-byte header (magic, unknown, file count, unknown), then one 32-byte
    // entry per file (16-byte name, unknown, encryption key, data offset, size). File data
    // follows the entry table.
    // ---
    // DPMX の構造: 16 バイトのヘッダー(マジック、不明、ファイル数、不明)の後に、ファイル
    // ごとに 32 バイトのエントリ(16 バイトの名前、不明、暗号鍵、データ位置、サイズ)が並ぶ。
    // ファイルデータはエントリ表の後ろに続く。
    private bool ReadDpmxHeader()
    {
        _reader!.ReadInt32();
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
            if ((file.FileOffset + file.FileSize) > _streamLength)
            {
                return false;
            }

            _files.Add(file);
        }

        return true;
    }

    // DPM2 layout: a 32-byte header (magic, entry count, string table offset, data section
    // offset, string table size, unknown, CRC seed, salt), then 32-byte entries (flags, name
    // offset, size, unknown, data offset, unknown, directory offset, checksum). Offsets in the
    // header count from the header start; name and directory offsets index the table of
    // NUL-terminated strings, and a directory offset of 0 means the archive root. The entry
    // checksum seeds the per-file key table.
    // ---
    // DPM2 の構造: 32 バイトのヘッダー(マジック、エントリ数、文字列表の位置、データ部の位置、
    // 文字列表のサイズ、不明、CRC シード、ソルト)の後に、32 バイトのエントリ(フラグ、名前の
    // 位置、サイズ、不明、データ位置、不明、ディレクトリの位置、チェックサム)が並ぶ。
    // ヘッダー内の位置はヘッダー先頭からの値で、名前とディレクトリの位置は NUL 終端文字列の
    // 表を指す。ディレクトリの位置が 0 ならアーカイブ直下を表す。エントリのチェックサムは
    // ファイルごとの鍵表のシードになる。
    private bool ReadDpm2Header()
    {
        _isDpm2 = true;
        _reader!.ReadInt32();
        int fileCount = _reader.ReadInt32();
        int stringTableOffset = _reader.ReadInt32();
        int dataSectionOffset = _reader.ReadInt32();
        _reader.ReadInt32();
        _reader.ReadInt32();
        _crcSeed = _reader.ReadInt32();
        _salt = _reader.ReadInt32();

        _files.Capacity = fileCount;

        long stringTableAbsOffset = _startPosition + stringTableOffset;
        long savedPosition = _reader.BaseStream.Position;
        _reader.BaseStream.Seek(stringTableAbsOffset, SeekOrigin.Begin);

        int stringTableSize = dataSectionOffset - stringTableOffset;
        byte[] stringTableBytes = new byte[stringTableSize];
        _reader.BaseStream.ReadExactly(stringTableBytes, 0, stringTableSize);

        _reader.BaseStream.Seek(savedPosition, SeekOrigin.Begin);

        _fileOffsetStart = _startPosition + dataSectionOffset;
        for (int i = 0; i < fileCount; i++)
        {
            var file = new DpmFileEntry();
            _reader.ReadInt32();
            int nameOffset = _reader.ReadInt32();
            file.FileSize = _reader.ReadInt32();
            _reader.ReadInt32();
            file.FileOffset = _reader.ReadInt32();
            _reader.ReadInt32();
            int dirOffset = _reader.ReadInt32();
            file.Checksum = _reader.ReadInt32();

            file.FileName = ReadStringFromTable(stringTableBytes, nameOffset);

            if (dirOffset != 0)
            {
                string dir = ReadStringFromTable(stringTableBytes, dirOffset);
                if (!string.IsNullOrEmpty(dir))
                {
                    file.FileName = dir + "\\" + file.FileName;
                }
            }

            long absoluteOffset = dataSectionOffset + (long)file.FileOffset;
            long absoluteEnd = absoluteOffset + file.FileSize;
            if (
                file.FileOffset < 0
                || file.FileSize < 0
                || absoluteOffset < dataSectionOffset
                || absoluteEnd > _streamLength
            )
            {
                return false;
            }

            _files.Add(file);
        }

        return true;
    }

    private static string ReadStringFromTable(byte[] table, int offset)
    {
        if (offset < 0 || offset >= table.Length)
        {
            return string.Empty;
        }
        int end = offset;
        while (end < table.Length && table[end] != 0)
        {
            end++;
        }
        return System.Text.Encoding.ASCII.GetString(table, offset, end - offset);
    }

    internal List<DpmFileEntry> FileList => _files;

    internal byte[] GetFile(int fileOffset, int fileSize)
    {
        _reader!.BaseStream.Seek(fileOffset, SeekOrigin.Begin);
        byte[] buffer = new byte[fileSize];
        _reader.BaseStream.ReadExactly(buffer, 0, fileSize);
        return buffer;
    }

    // HSP runtimes start the program from the archive's "start.ax" entry.
    // ---
    // HSP ランタイムはアーカイブ内の "start.ax" からプログラムを開始する。
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
