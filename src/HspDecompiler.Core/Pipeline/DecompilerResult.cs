using System.Collections.Generic;
using HspDecompiler.Core.DpmToAx;

namespace HspDecompiler.Core.Pipeline;

public sealed class DecompilerResult
{
    public bool Success { get; set; }
    public string? OutputPath { get; set; }
    public List<string> Warnings { get; } = [];
    public List<DpmFileEntry> DpmFiles { get; } = [];
    public string? ErrorMessage { get; set; }
}
