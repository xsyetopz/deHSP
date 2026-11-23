namespace HspDecompiler.Core.Ax3.Data.PP;

internal abstract class Preprocessor
{
    protected Preprocessor() { }

    protected Preprocessor(int index)
    {
        _index = index;
    }

    protected readonly int _index;
    public abstract override string ToString();

    // The declaration as the line inside the given context writes it; only names that depend
    // on the module differ from ToString.
    //
    // 与えた文脈の中の行が書く宣言。モジュールに依存する名前だけが ToString と異なる。
    internal virtual string Render(RenderContext context) => ToString();
}
