using System.Collections.Generic;
using HspDecompiler.Core.Ax3.Data.PP;
using HspDecompiler.Core.Encoding;
using HspDecompiler.Core.Keywords;
using HspDecompiler.Core.Pipeline;
using HspDecompiler.Core.Releases;

namespace HspDecompiler.Core.Ax3.Data;

// The program one AX3 image holds once read: its header, object tables, runtime, debug segment
// and script strings, plus what the passes learn about it. Ax3ImageReader fills it and
// NameAssigner names it; nothing here reads a stream.
//
// 読み終えた 1 つの AX3 イメージが持つプログラム。ヘッダー、オブジェクトテーブル、ランタイム、
// デバッグセグメント、スクリプトの文字列と、各段が知ったこと。Ax3ImageReader が埋め、
// NameAssigner が名前を付ける。ここではストリームを読まない。
internal class AxData
{
    internal KeywordDictionary? Dictionary { get; init; }

    internal AxHeader? Header { get; set; }

    internal DebugInfo DebugInfo { get; set; } = new();

    internal ModuleScope Scope { get; } = new();

    internal SourceStyle Style { get; init; } = SourceStyle.Default;

    // The releases that could have written this AX: every release until the header is read,
    // the releases of its format word until the code is read, then the inferred ones.
    //
    // この AX を書いた可能性のあるリリース。ヘッダーを読むまではすべてのリリース、コードを
    // 読むまでは形式のワードのリリース、その後は推定したリリース。
    internal ReleaseProfile Release { get; set; } = ReleaseProfile.Unknown;

    internal List<Function> Modules { get; } = [];

    internal List<PlugIn> PlugIns { get; } = [];

    internal Runtime? Runtime { get; set; }

    internal List<Usedll> Usedlls { get; } = [];

    // Literal-data offset and code offset of every string and double literal, in code order.
    //
    // コード中の文字列・実数リテラルごとのリテラルデータ位置とコード位置(コード順)。
    internal List<(int DataOffset, int TokenOffset)> LiteralAnchors { get; } = [];

    internal bool HasSingleItemFlags { get; set; }

    // The releases that have every keyword the code uses (LexicalAnalyzer.NoteKeyword).
    //
    // コードが使うすべてのキーワードを持つリリース(LexicalAnalyzer.NoteKeyword)。
    internal ReleaseSpan KeywordSpan { get; set; } = ReleaseSpan.All;

    internal List<Label> Labels { get; private set; } = [];

    internal List<Function> Functions { get; } = [];

    internal List<Param> FunctionParams { get; } = [];

    internal ScriptStrings ScriptStrings { get; } = new();

    // Set when one non-empty string sits at two literal offsets, which hspcmp writes only with
    // optcode off (Ax3ImageReader).
    //
    // 空でない同じ文字列が 2 つのリテラル位置にあると立つ。hspcmp がそう書くのは optcode を
    // 切った時だけ(Ax3ImageReader)。
    internal bool HasRepeatedStrings { get; set; }

    // Set when the code calls await. OpenHSP's hspcmp then leaves the NOMMTIMER boot bit clear,
    // and sets it otherwise (codegen.cpp hed_autoopt_timer).
    //
    // コードが await を呼ぶと立つ。OpenHSP の hspcmp はその場合 NOMMTIMER 起動ビットを立てず、
    // それ以外では立てる(codegen.cpp の hed_autoopt_timer)。
    internal bool UsesAwait { get; set; }

    internal Label GetLabel(int index) => Labels[index];

    internal Function? GetFunction(int index) =>
        index < 0 ? null
        : index >= Functions.Count ? null
        : Functions[index];

    internal Usedll? GetUsedll(int index) =>
        index < 0 ? null
        : index >= Usedlls.Count ? null
        : Usedlls[index];

    internal Param? GetParam(int index) =>
        index < 0 ? null
        : index >= FunctionParams.Count ? null
        : FunctionParams[index];

    internal bool HasVariableNames => DebugInfo.VariableNames.Count != 0;

    internal string? GetVariableName(int index) =>
        index < 0 ? null
        : index >= DebugInfo.VariableNames.Count ? null
        : ScriptStrings.Encoding.GetString(DebugInfo.VariableNames[index]);

    internal string? GetDebugName(Dictionary<int, byte[]> names, int index) =>
        names.TryGetValue(index, out byte[]? name) ? DecodeName(name) : null;

    // Names stored through PutDSBuf keep the source encoding, which is UTF-8 only when the
    // strings are UTF-8 too and the bytes hold valid UTF-8.
    //
    // PutDSBuf で格納された名前はソースのエンコーディングのままで、文字列も UTF-8 であり
    // バイト列が正しい UTF-8 の時だけ UTF-8 になる。
    internal string DecodeName(byte[] name) =>
        ScriptStrings.IsUtf8 && System.Text.Unicode.Utf8.IsValid(name)
            ? ScriptStrings.Encoding.GetString(name)
            : ShiftJisHelper.Encoding.GetString(name);

    internal Cmd? AddCmd(int pluginIndex, int methodIndex) =>
        pluginIndex < 0 ? null
        : pluginIndex >= PlugIns.Count ? null
        : PlugIns[pluginIndex].AddCmd(methodIndex);

    internal void DeleteInvisibleLabels() => Labels = Labels.FindAll(label => label.Visible);
}
