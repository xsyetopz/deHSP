using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace HspDecompiler.Core.Pipeline;

// The caller's HSP common files (the directory hspcmp --compath names), read by the name a debug
// build recorded for each #include. The decoder reads them only through this, so a decode of
// bytes in memory touches no disk unless the caller hands it a directory.
//
// 呼び出し側の HSP 共通ファイル(hspcmp の --compath が指すディレクトリ)。デバッグビルドが
// 各 #include に記録した名前で読む。デコーダーはこれを通してしか読まないため、メモリ上の
// バイト列の復元は、呼び出し側がディレクトリを渡さない限りディスクに触れない。
internal interface ICommonFiles
{
    bool TryRead(string name, [NotNullWhen(true)] out byte[]? bytes);
}

internal sealed class DirectoryCommonFiles(string directory) : ICommonFiles
{
    // hspcmp records a subdirectory with '\\', which the host path may not use.
    //
    // hspcmp はサブディレクトリを '\\' で記録するが、ホストのパスはそれを使わないことがある。
    public bool TryRead(string name, [NotNullWhen(true)] out byte[]? bytes)
    {
        string path = Path.Combine(directory, name.Replace('\\', Path.DirectorySeparatorChar));
        bytes = File.Exists(path) ? File.ReadAllBytes(path) : null;
        return bytes != null;
    }

    internal static ICommonFiles? Of(string? directory) =>
        string.IsNullOrEmpty(directory) ? null : new DirectoryCommonFiles(directory);
}
