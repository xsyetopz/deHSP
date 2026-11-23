using System.Collections.Generic;

namespace HspDecompiler.Core.Releases;

// How a compiler build lays out a user function's parameters on the stack (STRUCTPRM.offset,
// STRUCTDAT.size). Every 32-bit hspcmp packs pointers in 4 bytes. The 64-bit hspcmp.exe of
// 3.8β2 packs them in 8, with or without -x and whatever the runtime, so the spacing names the
// compiler binary even where the code is the same.
//
// コンパイラのビルドがユーザー関数の引数をスタックに並べるやり方(STRUCTPRM.offset、
// STRUCTDAT.size)。32 ビットの hspcmp はどれもポインターを 4 バイトに詰める。3.8β2 の 64 ビットの
// hspcmp.exe は -x の有無やランタイムにかかわらず 8 バイトに詰めるため、コードが同じでも間隔が
// コンパイラのバイナリを示す。
internal enum FrameLayout
{
    Narrow,
    Wide,
}

// Reads the frame layout from the parameter sizes. The sizes were measured by compiling one
// #deffunc per parameter type with hspcmp 3.1 to 3.7, the 3.8β2 hspcmp.dll and the 3.8β2
// hspcmp.exe; int, double, str and label take the same room in both layouts.
//
// 引数のサイズからフレームの並べ方を読む。サイズは引数型ごとの #deffunc を hspcmp 3.1〜3.7、
// 3.8β2 の hspcmp.dll と hspcmp.exe でコンパイルして測った。int・double・str・label はどちらの
// 並べ方でも同じ大きさを取る。
internal static class FrameLayouts
{
    private static readonly Dictionary<short, (int Narrow, int Wide)> s_sizes = new()
    {
        [3] = (8, 8), // double
        [4] = (4, 4), // int
        [7] = (4, 4), // label
        [-1] = (48, 64), // local
        [-2] = (8, 16), // array
        [-3] = (8, 16), // var
        [-6] = (4, 4), // str
        [-7] = (12, 24), // modvar (thismod of #modfunc)
        [-11] = (12, 24), // thismod of #modinit
    };

    // The layout one function's frame shows, or null when every parameter takes the same room
    // in both layouts or a parameter type was not measured.
    //
    // 1 つの関数のフレームが示す並べ方。すべての引数がどちらでも同じ大きさを取るか、測って
    // いない引数型がある時は null。
    internal static FrameLayout? Of(IEnumerable<short> parameterTypes, int frameSize)
    {
        int narrow = 0;
        int wide = 0;
        foreach (short type in parameterTypes)
        {
            if (!s_sizes.TryGetValue(type, out (int Narrow, int Wide) size))
            {
                return null;
            }

            narrow += size.Narrow;
            wide += size.Wide;
        }

        return narrow == wide ? null
            : frameSize == wide ? FrameLayout.Wide
            : frameSize == narrow ? FrameLayout.Narrow
            : null;
    }
}
