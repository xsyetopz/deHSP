using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Pipeline;
using Xunit;

namespace HspDecompiler.Core.Tests.IntegrationTests;

// Each style spells the same program differently, and every expected file recompiles to the
// fixture's AX byte for byte: styles.ax with hspcmp 3.7, dbgcolon.ax with hspcmp 3.7 -d.
//
// スタイルごとに同じプログラムを別の綴りで書き、期待ファイルはどれもフィクスチャの AX に
// バイト単位で戻る。styles.ax は hspcmp 3.7、dbgcolon.ax は hspcmp 3.7 -d でコンパイルする。
public class SourceStyleTests
{
    private static readonly Dictionary<string, SourceStyle> s_styles = new()
    {
        ["default"] = new SourceStyle(),
        ["compact"] = new SourceStyle
        {
            OneLineIf = OneLineIf.Braces,
            IfParentheses = IfParentheses.Tight,
            LogicalOperators = LogicalOperators.Doubled,
            CompactOperators = true,
            ParenthesizeNegatives = true,
            HexLiterals = HexLiterals.All,
            StandardMacros = false,
        },
        ["words"] = new SourceStyle
        {
            IfParentheses = IfParentheses.Spaced,
            LogicalOperators = LogicalOperators.Words,
            HexLiterals = HexLiterals.Poke,
        },
        ["braces"] = new SourceStyle { OneLineIf = OneLineIf.Braces },
    };

    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Ax", name);

    [Theory]
    [InlineData("styles", "default", "styles.expected.hsp")]
    [InlineData("styles", "compact", "styles.compact.expected.hsp")]
    [InlineData("styles", "words", "styles.words.expected.hsp")]
    [InlineData("dbgcolon", "default", "dbgcolon.expected.hsp")]
    [InlineData("dbgcolon", "braces", "dbgcolon.braces.expected.hsp")]
    public async Task StyleSpellsTheProgramAsExpected(string fixture, string style, string expected)
    {
        string root = Path.Combine(Path.GetTempPath(), $"hsp-decompiler-test-{Guid.NewGuid():N}");
        var pipeline = new DecompilerPipeline(new NullLogger(), new NullProgressReporter());
        Assert.True(pipeline.Initialize());
        var options = new DecompilerOptions
        {
            InputPath = Fixture($"{fixture}.ax"),
            OutputDirectory = root,
            Style = s_styles[style],
        };

        try
        {
            DecompilerResult result = await pipeline.RunAsync(options, CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(
                await File.ReadAllBytesAsync(Fixture(expected)),
                await File.ReadAllBytesAsync(result.OutputPath!)
            );
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
