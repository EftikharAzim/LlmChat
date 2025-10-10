namespace LlmChat.Tools;

public sealed record ToolResult(bool Ok, object? Data, string? Error = null);
