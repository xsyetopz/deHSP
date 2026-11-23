using System;
using System.Collections.Generic;
using System.Linq;

namespace HspDecompiler.Core.Ax3.Data.Layout;

// Writes the items of a normal build so that each #module, #global and {"..."} string stands on
// its recorded line. The rows between two of them keep a line each while they fit; otherwise
// blank rows drop, and then code joins the line before it, earliest first. The directives the
// source opens with go first when they fit before the first recorded line, and after the code
// otherwise, since hspcmp reads them before it generates any code.
//
// 通常ビルドの項目を、各 #module・#global・{"..."} 文字列が記録された行に立つように書く。
// その間の行は収まる限り 1 行ずつ保ち、収まらなければ空行を落とし、さらにコードを前の行に
// 先頭からつなぐ。ソースの先頭の指示文は最初の記録の行より前に収まれば先頭に、そうでなければ
// コードの後に置く。hspcmp はコードを生成する前にそれらを読むためである。
internal sealed class MarkedPlacer(RenderContext context, List<string> headers)
{
    internal List<List<string>>? Place(List<MarkedItem> items, Func<int, string> partName)
    {
        var renderer = new LineRenderer(context);
        List<Slot> rendered =
        [
            .. items.Select(item => new Slot(
                renderer.Render(item.Line),
                item.Line.TabCount,
                item.Kind
            )),
        ];
        return Run(items, rendered, partName, true) ?? Run(items, rendered, partName, false);
    }

    private List<List<string>>? Run(
        List<MarkedItem> items,
        List<Slot> rendered,
        Func<int, string> partName,
        bool headersFirst
    )
    {
        var parts = new List<List<string>>();
        var part = new Part();
        List<Slot> pending = headersFirst
            ? [.. headers.Select(h => new Slot(h, 0, LineKind.Header))]
            : [];
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].StartsPart)
            {
                part.Close(pending, partName(parts.Count + 1));
                parts.Add(part.Lines);
                part = new Part();
            }

            bool placed =
                items[i].Target > 0 ? part.PlaceDirective(pending, rendered[i], items[i].Target)
                : items[i].Ends is { } ends
                    ? part.PlaceString(pending, rendered[i], ends, items[i].Escapes)
                : Defer(pending, rendered[i]);
            if (!placed)
            {
                return null;
            }
        }

        part.Close(pending, null);
        parts.Add(part.Lines);
        if (!headersFirst)
        {
            parts[0].AddRange(headers);
        }

        return parts;
    }

    private static bool Defer(List<Slot> pending, Slot slot)
    {
        pending.Add(slot);
        return true;
    }

    // One line of output, or a row waiting for its line.
    //
    // 出力の 1 行、または自分の行を待つ行。
    private sealed record Slot(string Text, int Tabs, LineKind Kind)
    {
        internal string Line => new string('\t', Tabs) + Text;

        internal bool TakesJoin => Kind.TakesJoin();

        internal bool LetsJoin => Kind.LetsJoin();

        internal Slot Join(Slot next) =>
            this with
            {
                Text = FilePlacer.Join(Text, next.Text),
                Kind = next.Kind,
            };
    }

    // The lines of one file and whether code may join the last of them.
    //
    // 1 つのファイルの行と、その最後の行にコードをつなげるかどうか。
    private sealed class Part
    {
        private Slot? _last;

        internal List<string> Lines { get; } = [];

        internal void Close(List<Slot> pending, string? include)
        {
            Lines.AddRange(pending.Select(slot => slot.Line));
            pending.Clear();
            if (include != null)
            {
                Lines.Add($"#include \"{include}\"");
            }
        }

        internal bool PlaceDirective(List<Slot> pending, Slot directive, int target)
        {
            if (!Fill(pending, target - 1 - Lines.Count, false))
            {
                return false;
            }

            Lines.Add(directive.Line);
            _last = null;
            return true;
        }

        // A string row starts as late as its first recorded end allows, so the rows before it
        // keep their lines, and joins the line before it when they cannot.
        //
        // 文字列の行は最初の記録された終わりが許す限り遅く始め、前の行が行を保てるようにする。
        // 保てなければ前の行につなぐ。
        internal bool PlaceString(
            List<Slot> pending,
            Slot row,
            SortedDictionary<int, int> ends,
            int[] escapes
        )
        {
            (int ordinal, int end) = ends.First();
            int earliest = end - escapes[ordinal];
            int start = Math.Max(earliest, Math.Min(end, Lines.Count + 1 + pending.Count));
            bool joins = false;
            if (!Fill(pending, start - 1 - Lines.Count, false))
            {
                start = Math.Max(earliest, Lines.Count);
                while (start <= end && !Fill(pending, start - Lines.Count, true))
                {
                    start++;
                }

                if (start > end)
                {
                    return false;
                }

                joins = true;
            }

            var breaks = new Dictionary<int, int>();
            int previous = start;
            foreach ((int at, int line) in ends)
            {
                breaks[at] = line - previous;
                previous = line;
            }

            string text = LongStrings.RenderAt(joins ? row.Text : row.Line, breaks);
            Write(joins ? FilePlacer.Join(Lines[^1], text) : text, joins);
            _last = row;
            return Lines.Count == previous;
        }

        private void Write(string text, bool replacesLast)
        {
            if (replacesLast)
            {
                Lines.RemoveAt(Lines.Count - 1);
            }

            Lines.AddRange(text.Split('\n'));
        }

        // Writes the waiting rows in exactly count lines. With openEnd the last of them must let
        // code join it, because the row that comes next joins that line.
        //
        // 待っている行をちょうど count 行で書く。openEnd の時は、次の行がその最後の行につながる
        // ため、最後の行はコードをつなげられなければならない。
        private bool Fill(List<Slot> pending, int count, bool openEnd)
        {
            if (count < 0)
            {
                return false;
            }

            bool joinsLast = _last is { LetsJoin: true } && Lines.Count > 0;
            List<Slot> slots = joinsLast
                ? [_last! with { Text = Lines[^1] }, .. pending]
                : [.. pending];
            int target = count + (joinsLast ? 1 : 0);
            if (Pack(slots, target) is not { } packed || (openEnd && !EndsOpen(packed)))
            {
                return false;
            }

            int blanks = target - packed.Count;
            int at = openEnd ? packed.Count - 1 : packed.Count;
            if (blanks > 0 && at < (joinsLast ? 1 : 0))
            {
                return false;
            }

            packed.InsertRange(at, Enumerable.Repeat(s_blank, blanks));
            if (joinsLast)
            {
                Lines.RemoveAt(Lines.Count - 1);
                packed[0] = packed[0] with { Tabs = 0 };
            }

            Lines.AddRange(packed.Select(slot => slot.Line));
            _last = packed.Count > 0 ? packed[^1] : _last;
            pending.Clear();
            return true;
        }

        private static readonly Slot s_blank = new(string.Empty, 0, LineKind.Blank);

        private static bool EndsOpen(List<Slot> packed) => packed.Count > 0 && packed[^1].LetsJoin;

        // The rows in at most count lines: as they are, without blank rows, or with code joined
        // to the line before it, earliest first. The first slot may be a written line, which
        // stays first.
        //
        // 行を count 行以内に収める。そのまま、空行を除いて、またはコードを前の行に先頭から
        // つないで収める。最初の項目は書いた行のことがあり、その時は先頭に残る。
        private static List<Slot>? Pack(List<Slot> slots, int count)
        {
            if (slots.Count <= count)
            {
                return slots;
            }

            List<Slot> filled = [.. slots.Where(slot => slot.Kind != LineKind.Blank)];
            int lines = filled.Count;
            var packed = new List<Slot>();
            foreach (Slot slot in filled)
            {
                if (lines > count && packed.Count > 0 && slot.TakesJoin && packed[^1].LetsJoin)
                {
                    packed[^1] = packed[^1].Join(slot);
                    lines--;
                    continue;
                }

                packed.Add(slot);
            }

            return lines <= count ? packed : null;
        }
    }
}
