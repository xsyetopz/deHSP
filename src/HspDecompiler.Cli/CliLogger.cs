using System;
using System.Collections.Generic;
using System.IO;
using HspDecompiler.Core.Abstractions;

namespace HspDecompiler.Cli;

internal sealed class CliLogger(bool verbose, TextWriter output, TextWriter error)
    : IDecompilerLogger
{
    private readonly List<string> _warnings = [];
    private readonly int _startTime = Environment.TickCount;
    private int _indentLevel;

    public IReadOnlyList<string> Warnings => _warnings;

    public void Write(string message)
    {
        int elapsed = Environment.TickCount - _startTime;
        string indent = new(' ', _indentLevel * 2);
        output.WriteLine($"[{elapsed, 8}] {indent}{message}");
    }

    public void Warning(string message, int lineNumber = -1)
    {
        string warning = lineNumber >= 0 ? $"{lineNumber:D6}: {message}" : message;
        _warnings.Add(warning);
        if (verbose)
        {
            error.WriteLine($"WARNING: {warning}");
        }
    }

    public void LogError(string message) => error.WriteLine($"ERROR: {message}");

    public void LogError(Exception exception) => error.WriteLine($"ERROR: {exception.Message}");

    public void StartSection() => _indentLevel++;

    public void EndSection() => _indentLevel--;
}
