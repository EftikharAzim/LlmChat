using LlmChat.Abstractions;

namespace LlmChat.Wpf.Services;

public sealed class FallbackChatClient : IChatClient
{
    private readonly string _reason;
    public FallbackChatClient(string reason) => _reason = reason;

    public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
        => Task.FromResult(new ChatResponse($"[Fallback] {_reason}"));
}
