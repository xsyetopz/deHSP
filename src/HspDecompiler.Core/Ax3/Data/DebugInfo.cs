using System;
using System.Collections.Generic;
using System.IO;

namespace HspDecompiler.Core.Ax3.Data;

// One record of the line table: the code that hspcmp emitted for one source line. A line that
// holds {"..."} strings lists the line each of them ended on (codegen.cpp PickLongStringCG).
//
// 行テーブルの 1 レコード。hspcmp が 1 つのソース行に対して出力したコード。{"..."} 文字列を
// 含む行は、それぞれの文字列が終わった行を並べる(codegen.cpp の PickLongStringCG)。
internal readonly record struct DebugLine(
    int File,
    int Line,
    int CodeOffset,
    int CodeSize,
    int[]? StringEnds = null
);

// An #include that hspcmp expanded: the child file starts at the given line of the parent.
//
// hspcmp が展開した #include。親ファイルの指定行で子ファイルが始まる。
internal readonly record struct DebugInclude(int Parent, int Line, int Child);

// Reads the debug segment that hspcmp writes (codegen.cpp CToken::PutDI and its callers).
// After the code it holds, in order: one byte per preprocessed line with the code size of
// that line (252 and a 16-bit size when it exceeds 250), 254 records that set the file and
// line, 253 records with variable names, then a 255 byte and 251 records that name labels by
// object-table index, a 255 byte and 251 records that name parameters by STRUCTPRM index,
// and 255 255 at the end. Only debug mode (hspcmp -d) writes lines, labels and parameters;
// varname alone writes just the variable names.
//
// The preprocessor writes a "##N file" line when a file starts and when an include returns,
// and "##N" when the line count changed; hspcmp turns each into a 254 record, and the line
// it sits on gets an empty size byte of its own. A multiline string writes a 254 record in
// the middle of a line instead, so the next size byte is not empty. The first record names
// hspdef.as, which takes literal offset 0, so offset 0 means "no file" only after it.
//
// hspcmp が書き出すデバッグセグメント(codegen.cpp の CToken::PutDI と呼び出し元)を読む。
// コード部分に続いて順に、前処理後の 1 行ごとにその行のコード量を表す 1 バイト(250 を
// 超えると 252 と 16 ビットの量)、ファイルと行を設定する 254 レコード、変数名の 253
// レコード、255 の後にオブジェクトテーブル番号でラベルを名付ける 251 レコード、255 の後に
// STRUCTPRM 番号で引数を名付ける 251 レコード、最後に 255 255 が並ぶ。行・ラベル・引数を
// 書くのはデバッグモード(hspcmp -d)だけで、varname だけなら変数名しか書かない。
//
// プリプロセッサはファイルの開始時と #include から戻る時に "##N file" 行を、行数が
// 変わった時に "##N" 行を書く。hspcmp はそれぞれを 254 レコードにし、その行自身にも空の
// コード量バイトが付く。複数行文字列は行の途中で 254 レコードを書くため、次のコード量
// バイトは空にならない。最初のレコードはリテラル位置 0 を占める hspdef.as を指すため、
// 位置 0 が「ファイル無し」を意味するのはそれ以降だけである。
internal sealed class DebugInfo
{
    private const byte MarkerName = 251;
    private const byte MarkerBigOffset = 252;
    private const byte MarkerVariable = 253;
    private const byte MarkerFile = 254;
    private const byte MarkerEnd = 255;
    private const int RecordSize = 6;

    private readonly List<byte[]> _fileNames = [];
    private readonly List<int> _stack = [];

    // Reading state: the current file and line, the code offset of the next line, the line a
    // {"..."} string started on, the line ends of its strings, and where the current "##N"
    // segment of line records begins.
    //
    // 読み取り中の状態。現在のファイルと行、次の行のコード位置、{"..."} 文字列が始まった行、
    // その文字列が終わった行、現在の "##N" 区間の行レコードが始まる位置。
    private readonly List<int> _stringEnds = [];
    private int _file = -1;
    private int _line;
    private int _code;
    private int _startLine = -1;
    private int _segment;

    internal List<byte[]> VariableNames { get; } = [];

    internal List<DebugLine> Lines { get; } = [];

    internal List<DebugInclude> Includes { get; } = [];

    internal Dictionary<int, byte[]> LabelNames { get; } = [];

    internal Dictionary<int, byte[]> ParameterNames { get; } = [];

    internal IReadOnlyList<byte[]> FileNames => _fileNames;

    // The line of every 254 record, in segment order. hspcmp 3.1 to 3.32 write a "##N" record
    // for each #module and #global and the end line of each {"..."} string (3.3 and later) even
    // without debug mode, where no size bytes give the records a code line.
    //
    // セグメント順に並べた各 254 レコードの行。hspcmp 3.1〜3.32 はデバッグモードでなくても
    // #module と #global ごとの "##N" レコードと、各 {"..."} 文字列の終わりの行(3.3 以降)を
    // 書く。そこではコード量バイトが無く、レコードはコード行に結び付かない。
    internal List<int> Marks { get; } = [];

    internal bool HasLines => Lines.Count != 0;

    // The file hspcmp compiled; hspdef.as comes before it.
    //
    // hspcmp がコンパイルしたファイル。hspdef.as はその前に来る。
    internal int MainFile => _fileNames.Count > 1 ? 1 : 0;

    // Variable names go through PutDS like script strings, the other names through PutDSBuf,
    // which keeps the source encoding (codegen.cpp PutDSStr).
    //
    // 変数名はスクリプト文字列と同じく PutDS を、それ以外の名前はソースのエンコーディングの
    // まま残す PutDSBuf を通る(codegen.cpp の PutDSStr)。
    internal static DebugInfo Read(
        byte[] segment,
        Func<int, byte[]> readScriptString,
        Func<int, byte[]> readLiteral
    )
    {
        var info = new DebugInfo();
        int position = info.ReadLines(segment, readScriptString, readLiteral);
        position = ReadNames(segment, position, readLiteral, info.LabelNames);
        _ = ReadNames(segment, position, readLiteral, info.ParameterNames);
        return info;
    }

    private int ReadLines(
        byte[] d,
        Func<int, byte[]> readScriptString,
        Func<int, byte[]> readLiteral
    )
    {
        int i = 0;
        while (i < d.Length && d[i] != MarkerEnd)
        {
            i = d[i] is MarkerFile or MarkerVariable or MarkerName
                ? ReadRecord(d, i, readScriptString, readLiteral)
                : ReadSize(d, i);
        }

        return i;
    }

    // A truncated record ends the segment, so the name tables after it are not read.
    //
    // 途中で切れたレコードはセグメントの終わりとし、その後の名前テーブルは読まない。
    private int ReadRecord(
        byte[] d,
        int i,
        Func<int, byte[]> readScriptString,
        Func<int, byte[]> readLiteral
    )
    {
        if (i + RecordSize > d.Length)
        {
            return d.Length;
        }

        byte marker = d[i];
        int offset = d[i + 1] | (d[i + 2] << 8) | (d[i + 3] << 16);
        int value = d[i + 4] | (d[i + 5] << 8);
        i += RecordSize;
        if (marker == MarkerVariable)
        {
            VariableNames.Add(readScriptString(offset));
        }
        else if (marker == MarkerFile)
        {
            Marks.Add(value);
            SetFile(offset, value, i < d.Length && d[i] != 0, readLiteral);
        }

        return i;
    }

    private void SetFile(int offset, int value, bool midLine, Func<int, byte[]> readLiteral)
    {
        if (midLine && _file >= 0)
        {
            _startLine = _startLine < 0 ? _line : _startLine;
            _stringEnds.Add(value);
        }
        else if (offset != 0 || _file < 0)
        {
            _file = EnterFile(readLiteral(offset), value);
        }
        else
        {
            Rewind(_segment, value);
        }

        _line = value;
        _segment = Lines.Count;
    }

    private int ReadSize(byte[] d, int i)
    {
        int size = d[i];
        if (size == MarkerBigOffset)
        {
            if (i + 3 > d.Length)
            {
                return d.Length;
            }

            size = d[i + 1] | (d[i + 2] << 8);
            i += 3;
        }
        else
        {
            i++;
        }

        int[]? ends = _stringEnds.Count == 0 ? null : [.. _stringEnds];
        Lines.Add(new DebugLine(_file, _startLine < 0 ? _line : _startLine, _code, size, ends));
        _stringEnds.Clear();
        _code += size;
        _startLine = -1;
        _line++;
        return i;
    }

    // A directive that expands to several lines (#module adds "goto *_name_exit", #global adds
    // "*_name_exit") is followed by "##N" with the line it stood on, so the line count goes
    // back. The expanded lines all come from that source line.
    //
    // 複数行に展開される命令(#module は "goto *_名前_exit" を、#global は "*_名前_exit" を
    // 足す)の後には、それがあった行を示す "##N" が続き、行数が戻る。展開された行はすべて
    // その元の行から来ている。
    private void Rewind(int segment, int line)
    {
        for (int i = segment; i < Lines.Count; i++)
        {
            if (Lines[i].Line > line)
            {
                Lines[i] = Lines[i] with { Line = line };
            }
        }
    }

    // A record with line 0 starts a file, and any other line returns from an include into the
    // named parent, whose #include stood on that line.
    //
    // 行 0 のレコードはファイルの開始で、それ以外の行は指定の親ファイルへ #include から
    // 戻ったことを表す。その #include は親のその行にあった。
    private int EnterFile(byte[] name, int line)
    {
        // Without optcode every record stores the name again, so files compare by name.
        //
        // optcode が無いとレコードごとに名前が再格納されるため、ファイルは名前で比べる。
        int file = _fileNames.FindIndex(known => known.AsSpan().SequenceEqual(name));
        if (file < 0)
        {
            file = _fileNames.Count;
            _fileNames.Add(name);
        }

        if (line == 0)
        {
            // hspdef.as ends without a return record before the main file starts.
            //
            // hspdef.as はメインファイルの開始前に戻りレコード無しで終わる。
            if (_stack.Count == 1 && _stack[0] == 0)
            {
                _stack.Clear();
            }

            _stack.Add(file);
            return file;
        }

        int parentIndex = _stack.LastIndexOf(file);
        if (parentIndex >= 0 && parentIndex + 1 < _stack.Count)
        {
            Includes.Add(new DebugInclude(file, line, _stack[parentIndex + 1]));
            _stack.RemoveRange(parentIndex + 1, _stack.Count - parentIndex - 1);
        }

        return file;
    }

    private static int ReadNames(
        byte[] d,
        int position,
        Func<int, byte[]> readLiteral,
        Dictionary<int, byte[]> names
    )
    {
        if (position >= d.Length || d[position] != MarkerEnd)
        {
            return d.Length;
        }

        int i = position + 1;
        while (i + RecordSize <= d.Length && d[i] == MarkerName)
        {
            int offset = d[i + 1] | (d[i + 2] << 8) | (d[i + 3] << 16);
            int id = d[i + 4] | (d[i + 5] << 8);
            names[id] = readLiteral(offset);
            i += RecordSize;
        }

        return i;
    }

    internal static byte[] ReadSegment(BinaryReader reader, long start, int size)
    {
        reader.BaseStream.Seek(start, SeekOrigin.Begin);
        return reader.ReadBytes(size);
    }
}
