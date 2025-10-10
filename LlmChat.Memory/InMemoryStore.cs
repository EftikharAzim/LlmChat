using System.Collections.Concurrent;

namespace LlmChat.Memory;

public sealed class InMemoryStore : IMemoryStore
{
    private readonly ConcurrentDictionary<string, List<(string Role, string Content)>> _msgs = new();
    private readonly ConcurrentDictionary<string, List<string>> _facts = new();

    public Task AppendAsync(string sessionId, string role, string content, CancellationToken ct = default)
    {
        var list = _msgs.GetOrAdd(sessionId, _ => new());
        lock (list) list.Add((role, content));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<(string Role, string Content)>> GetRecentAsync(string sessionId, int limit = 50, CancellationToken ct = default)
    {
        var list = _msgs.GetOrAdd(sessionId, _ => new());
        lock (list) return Task.FromResult((IReadOnlyList<(string, string)>)list.TakeLast(limit).ToList());
    }

    public Task AddFactAsync(string sessionId, string note, CancellationToken ct = default)
    {
        var list = _facts.GetOrAdd(sessionId, _ => new());
        lock (list) list.Add(note);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetFactsAsync(string sessionId, int limit = 10, CancellationToken ct = default)
    {
        var list = _facts.GetOrAdd(sessionId, _ => new());
        lock (list) return Task.FromResult((IReadOnlyList<string>)list.TakeLast(limit).ToList());
    }
}
