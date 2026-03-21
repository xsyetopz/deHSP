using System;
using System.Collections.Generic;
using HspDecompiler.Core.Abstractions;

namespace HspDecompiler.Core.Tests.IntegrationTests;

internal sealed class NullLogger : IDecompilerLogger
{
    private readonly List<string> _warnings = [];

    public IReadOnlyList<string> Warnings => _warnings;

    public void Write(string message) { }

    public void Warning(string message, int lineNumber = -1) => _warnings.Add(message);

    public void LogError(string message) { }

    public void LogError(Exception exception) { }

    public void StartSection() { }

    public void EndSection() { }
}
