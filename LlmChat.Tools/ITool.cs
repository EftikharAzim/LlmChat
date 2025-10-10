namespace LlmChat.Tools;

public interface ITool
{
    string Name { get; }               // e.g., "google.calendar.search"
    string Description { get; }
    string InputSchemaJson { get; }    // optional schema for planner
    string OutputSchemaJson { get; }

    Task<ToolResult> InvokeAsync(string sessionId,
        IReadOnlyDictionary<string, object> args,
        CancellationToken ct = default);
}
