using LlmChat.Abstractions;
using LlmChat.Providers.Gemini;

public static class ProviderFactory
{
    public static IChatClient Build()
    {
        var provider = Environment.GetEnvironmentVariable("LLM_PROVIDER")?.ToLowerInvariant() ?? "gemini";
        var model = Environment.GetEnvironmentVariable("LLM_MODEL");

        switch (provider)
        {
            case "gemini":
                var geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
                if (string.IsNullOrWhiteSpace(geminiKey))
                    throw new InvalidOperationException("GEMINI_API_KEY not set.");
                return new GeminiChatClient(new HttpClient(), geminiKey, model);

            default:
                throw new InvalidOperationException($"Unknown LLM_PROVIDER '{provider}'. Supported: 'gemini'.");
        }
    }
}