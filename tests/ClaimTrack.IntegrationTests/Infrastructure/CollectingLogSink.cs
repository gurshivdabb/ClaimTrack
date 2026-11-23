using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace ClaimTrack.IntegrationTests.Infrastructure;

// Keeps every Serilog event in memory so tests can check what the API logs.
// Program.cs calls ReadFrom.Services, which picks this up when it's registered in DI.
public sealed class CollectingLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyCollection<LogEvent> Events => _events;

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
}
