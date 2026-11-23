using System;
using System.Collections.Generic;

namespace HspDecompiler.Core.Abstractions;

internal sealed class NullDecompilerLogger : IDecompilerLogger
{
    internal static readonly NullDecompilerLogger Instance = new();

    private NullDecompilerLogger() { }

    public IReadOnlyList<string> Warnings => [];

    public void Write(string message) { }

    public void Warning(string message, int lineNumber = -1) { }

    public void LogError(string message) { }

    public void LogError(Exception exception) { }

    public void StartSection() { }

    public void EndSection() { }
}
