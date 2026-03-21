using System.Threading;
using System.Threading.Tasks;
using HspDecompiler.Core.Abstractions;

namespace HspDecompiler.Core.Tests.IntegrationTests;

internal sealed class NullProgressReporter : IProgressReporter
{
    public void Report(string status) { }

    public Task YieldAsync(CancellationToken ct = default) => Task.CompletedTask;
}
