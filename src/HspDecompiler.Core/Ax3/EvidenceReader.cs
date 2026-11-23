using System.Linq;
using HspDecompiler.Core.Ax3.Data;
using HspDecompiler.Core.Releases;

namespace HspDecompiler.Core.Ax3;

// Reads AxEvidence once, after the tokens are read and the script strings resolved: by then the
// lexer has seen every call and every flag, so no later pass changes a field.
//
// トークンを読み、スクリプトの文字列を決めた後に AxEvidence を一度だけ読む。その時点で字句
// 解析はすべての呼び出しと印を見ているため、後の段がどの欄も変えない。
internal static class EvidenceReader
{
    internal static AxEvidence Read(AxData data) =>
        new(
            data.Header!.Version,
            data.DebugInfo.HasLines ? 0 : data.DebugInfo.Marks.Count,
            data.HasSingleItemFlags,
            FramesOf(data)
        )
        {
            RepeatedStrings = data.HasRepeatedStrings,
            UncalledImport = DirectivePlanner.HasUncalledImport(data),
            VariableNames = data.HasVariableNames,
            DebugLines = data.DebugInfo.HasLines,
            BootOption = data.Header!.BootOption,
            Utf8Strings = data.ScriptStrings.IsUtf8,
            UsesAwait = data.UsesAwait,
        };

    // The parameter layout the first user function that tells the two apart shows.
    //
    // 2 つの並べ方を区別できる最初のユーザー関数が示す引数の並べ方。
    private static FrameLayout? FramesOf(AxData data) =>
        data
            .Functions.Where(f => f.IsUserFunction)
            .Select(f => FrameLayouts.Of(f.Parameters.Select(p => p.TypeCode), f.FrameSize))
            .FirstOrDefault(layout => layout != null);
}
