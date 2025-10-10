namespace LlmChat.Agent;

public interface IAgent
{
    Task<AgentTurnResult> HandleAsync(AgentTurnRequest req, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamAsync(AgentTurnRequest req, CancellationToken ct = default);
}

public sealed record AgentTurnRequest(string SessionId, string UserInput);
public sealed record AgentTurnResult(string FinalText, IReadOnlyList<ToolCallLog> ToolCalls);
public sealed record ToolCallLog(string ToolName, IReadOnlyDictionary<string, object> Args, bool Ok, string? Error);
