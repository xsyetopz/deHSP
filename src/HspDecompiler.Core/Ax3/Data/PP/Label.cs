using System.Collections.Generic;
using System.IO;

namespace HspDecompiler.Core.Ax3.Data.PP;

internal class Label : Preprocessor, System.IComparable<Label>
{
    private Label(int index)
        : base(index) { }

    private int _tokenOffset = -1;

    internal int Index => _index;

    internal int TokenOffset => _tokenOffset;

    internal static Label FromBinaryReader(BinaryReader reader, int index)
    {
        var ret = new Label(index) { _tokenOffset = reader.ReadInt32() };
        return ret;
    }

    private bool _visible;

    // A label is written when a printed statement uses it, when it starts a function, or when
    // no code token refers to it. hspcmp gives every *label definition an object-table slot
    // whether or not anything jumps there (codegen.cpp), so an unreferenced slot is a label
    // the author wrote; labels only repeat/foreach refer to are compiler-generated and stay hidden.
    //
    // ラベルは、出力される文が参照する時、関数の先頭である時、またはどのトークンからも
    // 参照されない時に出力する。hspcmp は *label 定義ごとにオブジェクトテーブルの枠を作る
    // (codegen.cpp)ため、参照の無い枠は作者が書いたラベルである。repeat/foreach だけが
    // 参照するラベルはコンパイラが生成したものなので出力しない。
    internal bool Visible
    {
        get => _function != null || _visible || !IsReferenced;
        set => _visible = value;
    }

    internal bool IsReferenced => References > 0;

    internal int References => _referenceOffsets.Count;

    private readonly List<int> _referenceOffsets = [];

    // The code offsets of the tokens that refer to the label.
    //
    // ラベルを参照するトークンのコード位置。
    internal IReadOnlyList<int> ReferenceOffsets => _referenceOffsets;

    internal void AddReference(int tokenOffset) => _referenceOffsets.Add(tokenOffset);

    private string _labelName = "*label";

    internal string LabelName
    {
        get => _labelName;
        set => _labelName = value;
    }

    // The rebuilt module the label was defined in; null when the output has no module blocks.
    //
    // ラベルを定義した再構築モジュール。出力にモジュールブロックが無ければ null である。
    internal string? Owner { get; set; }

    // The label's name as a line in the given context refers to it.
    //
    // 与えた文脈の行が参照する時のラベル名。
    internal string Name(RenderContext context) => context.Label(_labelName, Owner);

    internal override string Render(RenderContext context) =>
        _function != null ? _function.Render(context) : Name(context);

    public override string ToString() => _function != null ? _function.ToString() : _labelName;

    public int CompareTo(Label? other)
    {
        if (other == null)
        {
            return 1;
        }

        int ret = _tokenOffset.CompareTo(other._tokenOffset);
        return ret != 0 ? ret : _index.CompareTo(other._index);
    }

    private Function? _function;

    internal Function? Function => _function;

    internal void SetFunction(Function f)
    {
        _function = f;
        _visible = true;
    }
}
