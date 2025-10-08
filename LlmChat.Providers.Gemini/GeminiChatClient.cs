using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LlmChat.Abstractions;

namespace LlmChat.Providers.Gemini;

public sealed class GeminiChatClient : IChatClient
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly string _apiKey;
    private readonly string? _systemPrompt;

    public GeminiChatClient(HttpClient http, string apiKey, string? model = null, string? systemPrompt = null)
    {
        _http = http;
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _model = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model;
        _systemPrompt = systemPrompt;
        _http.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (_http.DefaultRequestHeaders.Contains("x-goog-api-key"))
            _http.DefaultRequestHeaders.Remove("x-goog-api-key");
        _http.DefaultRequestHeaders.Add("x-goog-api-key", _apiKey);
    }

    public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
    {
        var model = request.Model ?? _model;
        var endpoint = $"v1/models/{model}:generateContent";

        // Map our generic messages to Gemini "contents"
        var contents = new List<GeminiContent>();
        GeminiSystemInstruction? sys = null;
        
        if (!string.IsNullOrEmpty(_systemPrompt))
            sys = new GeminiSystemInstruction(new[] { new GeminiPart(_systemPrompt) });

        foreach (var msg in request.Messages)
        {
            if (msg.Role == ChatRole.System)
            {
                if (sys is null)
                    sys = new GeminiSystemInstruction(new[] { new GeminiPart(msg.Content) });
                continue; // don't include system messages in "contents"
            }
            contents.Add(new GeminiContent(
                Role: msg.Role == ChatRole.Assistant ? "model" : "user",
                Parts: new[] { new GeminiPart(msg.Content) }
            ));
        }

        var payload = new GeminiGenerateContentRequest(contents, sys);
        using var resp = await _http.PostAsJsonAsync(endpoint, payload, GeminiJson.Options, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Gemini API error {(int)resp.StatusCode} {resp.ReasonPhrase}: {err}");
        }

        if (resp.Content.Headers.ContentLength == 0)
        {
            throw new InvalidOperationException("Gemini returned an empty body.");
        }

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync<GeminiGenerateContentResponse>(stream, GeminiJson.Options, ct)
                     ?? throw new InvalidOperationException("Empty Gemini response.");

        // Pull first candidate text
        var text = result.Candidates?
                         .FirstOrDefault()?
                         .Content?.Parts?
                         .FirstOrDefault()?.Text ?? string.Empty;

        var finish = result.Candidates?.FirstOrDefault()?.FinishReason;
        return new ChatResponse(text, finish, result);
    }

    public async Task<string> SendMessageAsync(string prompt, CancellationToken ct = default)
    {
        var request = new ChatRequest(new List<ChatMessage> { new(ChatRole.User, prompt) });
        var response = await CompleteAsync(request, ct);
        return response.Text;
    }

    // === DTOs for Gemini JSON ===
    private record GeminiPart([property: JsonPropertyName("text")] string Text);
    private record GeminiContent(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("parts")] IEnumerable<GeminiPart> Parts);

    private record GeminiSystemInstruction(
        [property: JsonPropertyName("parts")] IEnumerable<GeminiPart> Parts);

    private record GeminiGenerateContentRequest(
        [property: JsonPropertyName("contents")] IEnumerable<GeminiContent> Contents,
        [property: JsonPropertyName("system_instruction")] GeminiSystemInstruction? SystemInstruction);

    private record GeminiCandidate(
        [property: JsonPropertyName("content")] GeminiContent Content,
        [property: JsonPropertyName("finishReason")] string? FinishReason);

    private record GeminiGenerateContentResponse(
        [property: JsonPropertyName("candidates")] IEnumerable<GeminiCandidate>? Candidates);

    private static class GeminiJson
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}