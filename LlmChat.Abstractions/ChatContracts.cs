namespace LlmChat.Abstractions;

public enum ChatRole { System, User, Assistant }

public record ChatMessage(ChatRole Role, string Content);

public record ChatRequest(
    IReadOnlyList<ChatMessage> Messages,
    string? Model = null,
    int? MaxTokens = null);

public record ChatResponse(
    string Text,
    string? FinishReason = null,
    object? Raw = null);

public interface IChatClient
{
    Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default);
}