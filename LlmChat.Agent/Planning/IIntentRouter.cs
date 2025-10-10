namespace LlmChat.Agent.Planning;

public interface IIntentRouter
{
    Task<IntentPlan> RouteAsync(AgentContext ctx, CancellationToken ct = default);
}

public sealed record IntentPlan(
    bool RequiresTool, string? ToolName,
    IReadOnlyDictionary<string, object>? Parameters,
    bool ShouldWriteMemory, string? MemoryNote
);

public sealed record AgentContext(
    string SessionId, string UserInput,
    IReadOnlyList<(string Role, string Content)> RecentMessages,
    LlmChat.Tools.IToolRegistry Tools
);