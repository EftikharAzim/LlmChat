namespace LlmChat.Memory;

public interface IMemoryStore
{
    Task AppendAsync(string sessionId, string role, string content, CancellationToken ct = default);
    Task<IReadOnlyList<(string Role, string Content)>> GetRecentAsync(string sessionId, int limit = 50, CancellationToken ct = default);
    Task AddFactAsync(string sessionId, string note, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetFactsAsync(string sessionId, int limit = 10, CancellationToken ct = default);
}
