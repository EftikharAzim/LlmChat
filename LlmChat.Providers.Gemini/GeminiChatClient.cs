using LlmChat.Abstractions;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        // Collect all system messages and combine them
        var systemMessages = new List<string>();

        if (!string.IsNullOrEmpty(_systemPrompt))
            systemMessages.Add(_systemPrompt);

        foreach (var msg in request.Messages)
        {
            if (msg.Role == ChatRole.System)
            {
                systemMessages.Add(msg.Content);
                continue; // don't include system messages in "contents"
            }
            contents.Add(new GeminiContent(
                Role: msg.Role == ChatRole.Assistant ? "model" : "user",
                Parts: new[] { new GeminiPart(msg.Content) }
            ));
        }

        // Combine all system messages into one
        if (systemMessages.Count > 0)
        {
            var combinedSystemMessage = string.Join("\n\n", systemMessages);
            sys = new GeminiSystemInstruction(new[] { new GeminiPart(combinedSystemMessage) });
        }

        var payload = new GeminiGenerateContentRequest(contents, sys);
        var json = JsonSerializer.Serialize(payload, GeminiJson.Options);
        using var resp = await _http.PostAsync(endpoint, new StringContent(json, Encoding.UTF8, "application/json"), ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            // Fallback: some deployments reject systemInstruction. Retry without it by prepending a user instruction.
            if ((int)resp.StatusCode == 400 && (err.Contains("SystemInstruction", StringComparison.OrdinalIgnoreCase) || err.Contains("systemInstruction", StringComparison.OrdinalIgnoreCase)))
            {
                var fallbackContents = new List<GeminiContent>();
                if (sys?.Parts is not null)
                {
                    // Flatten system instruction into first user message
                    var sysText = sys.Parts.FirstOrDefault()?.Text;
                    if (!string.IsNullOrWhiteSpace(sysText))
                        fallbackContents.Add(new GeminiContent("user", new[] { new GeminiPart(sysText!) }));
                }
                fallbackContents.AddRange(contents);
                var fallbackPayload = new GeminiGenerateContentRequest(fallbackContents, null);
                var fallbackJson = JsonSerializer.Serialize(fallbackPayload, GeminiJson.Options);
                using var resp2 = await _http.PostAsync(endpoint, new StringContent(fallbackJson, Encoding.UTF8, "application/json"), ct);
                if (!resp2.IsSuccessStatusCode)
                {
                    var err2 = await resp2.Content.ReadAsStringAsync(ct);
                    var showReq2 = Environment.GetEnvironmentVariable("LLMCHAT_DEBUG_HTTP") == "1";
                    var snippet2 = showReq2 ? $"\nRequest JSON (fallback): {fallbackJson}" : string.Empty;
                    throw new HttpRequestException($"Gemini API error {(int)resp2.StatusCode} {resp2.ReasonPhrase}: {err2}{snippet2}");
                }

                // Use the fallback response for parsing
                if (resp2.Content.Headers.ContentLength == 0)
                {
                    throw new InvalidOperationException("Gemini returned an empty body.");
                }

                using var stream2 = await resp2.Content.ReadAsStreamAsync(ct);
                var result2 = await JsonSerializer.DeserializeAsync<GeminiGenerateContentResponse>(stream2, GeminiJson.Options, ct)
                             ?? throw new InvalidOperationException("Empty Gemini response.");

                var text2 = result2.Candidates?
                                 .FirstOrDefault()?
                                 .Content?.Parts?
                                 .FirstOrDefault()?.Text ?? string.Empty;

                var finish2 = result2.Candidates?.FirstOrDefault()?.FinishReason;
                return new ChatResponse(text2, finish2, result2);
            }
            var showReq = Environment.GetEnvironmentVariable("LLMCHAT_DEBUG_HTTP") == "1";
            var snippet = showReq ? $"\nRequest JSON: {json}" : string.Empty;
            throw new HttpRequestException($"Gemini API error {(int)resp.StatusCode} {resp.ReasonPhrase}: {err}{snippet}");
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

    public async IAsyncEnumerable<string> StreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var model = request.Model ?? _model;
        var endpoint = $"v1/models/{model}:streamGenerateContent?alt=sse"; // SSE
        var payload = MapToGeminiPayload(request);

        async Task<HttpResponseMessage> SendAsync(GeminiGenerateContentRequest p)
        {
            var httpReq = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(p, options: GeminiJson.Options)
            };
            httpReq.Headers.Accept.Clear();
            httpReq.Headers.Accept.ParseAdd("text/event-stream");
            return await _http.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct);
        }

        using var resp = await SendAsync(payload);
        if (!resp.IsSuccessStatusCode)
        {
            var errorContent = await resp.Content.ReadAsStringAsync(ct);
            // Gemini v1 often rejects 'systemInstruction' – fallback by flattening system messages
            if ((int)resp.StatusCode == 400 &&
                (errorContent.Contains("systemInstruction", StringComparison.OrdinalIgnoreCase) ||
                 errorContent.Contains("Unknown name \"systemInstruction\"", StringComparison.OrdinalIgnoreCase)))
            {
                var fallbackPayload = MapToGeminiFallbackPayload(request);
                using var resp2 = await SendAsync(fallbackPayload);
                if (!resp2.IsSuccessStatusCode)
                {
                    var err2 = await resp2.Content.ReadAsStringAsync(ct);
                    throw new HttpRequestException($"Gemini API error {(int)resp2.StatusCode} {resp2.ReasonPhrase}: {err2}");
                }

                await using var stream2 = await resp2.Content.ReadAsStreamAsync(ct);
                using var reader2 = new StreamReader(stream2);

                string? line2;
                while ((line2 = await reader2.ReadLineAsync()) is not null)
                {
                    if (line2.StartsWith("data:"))
                    {
                        var json2 = line2["data:".Length..].Trim();
                        if (string.IsNullOrWhiteSpace(json2) || json2 == "[DONE]") continue;

                        var evt2 = JsonSerializer.Deserialize<GeminiStreamEvent>(json2, GeminiJson.Options);
                        var chunk2 = evt2?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
                        if (!string.IsNullOrEmpty(chunk2))
                            yield return chunk2!;
                    }
                }
                yield break;
            }
            throw new HttpRequestException($"Gemini API error {(int)resp.StatusCode} {resp.ReasonPhrase}: {errorContent}");
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (line.StartsWith("data:"))
            {
                var json = line["data:".Length..].Trim();
                if (string.IsNullOrWhiteSpace(json) || json == "[DONE]") continue;

                var evt = JsonSerializer.Deserialize<GeminiStreamEvent>(json, GeminiJson.Options);
                var chunk = evt?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
                if (!string.IsNullOrEmpty(chunk))
                    yield return chunk!;
            }
        }
    }

    // Build a payload that flattens all system messages into a first user turn (compatible with v1)
    private GeminiGenerateContentRequest MapToGeminiFallbackPayload(ChatRequest request)
    {
        var contents = new List<GeminiContent>();

        // Combine system prompts
        var systemMessages = new List<string>();
        if (!string.IsNullOrEmpty(_systemPrompt))
            systemMessages.Add(_systemPrompt);

        foreach (var msg in request.Messages)
        {
            if (msg.Role == ChatRole.System)
            {
                systemMessages.Add(msg.Content);
            }
        }

        if (systemMessages.Count > 0)
        {
            var combined = string.Join("\n\n", systemMessages);
            contents.Add(new GeminiContent("user", new[] { new GeminiPart(combined) }));
        }

        // Add the rest of the conversation (excluding system)
        foreach (var msg in request.Messages)
        {
            if (msg.Role == ChatRole.System) continue;
            contents.Add(new GeminiContent(
                Role: msg.Role == ChatRole.Assistant ? "model" : "user",
                Parts: new[] { new GeminiPart(msg.Content) }
            ));
        }

        return new GeminiGenerateContentRequest(contents, null);
    }

    private GeminiGenerateContentRequest MapToGeminiPayload(ChatRequest request)
    {
        var contents = new List<GeminiContent>();
        GeminiSystemInstruction? sys = null;

        // Collect all system messages and combine them
        var systemMessages = new List<string>();
        if (!string.IsNullOrEmpty(_systemPrompt))
            systemMessages.Add(_systemPrompt);

        foreach (var msg in request.Messages)
        {
            if (msg.Role == ChatRole.System)
            {
                systemMessages.Add(msg.Content);
                continue; // don't include system messages in "contents"
            }
            contents.Add(new GeminiContent(
                Role: msg.Role == ChatRole.Assistant ? "model" : "user",
                Parts: new[] { new GeminiPart(msg.Content) }
            ));
        }

        // Combine all system messages into one
        if (systemMessages.Count > 0)
        {
            var combinedSystemMessage = string.Join("\n\n", systemMessages);
            sys = new GeminiSystemInstruction(new[] { new GeminiPart(combinedSystemMessage) });
        }

        return new GeminiGenerateContentRequest(contents, sys);
    }

    private record GeminiStreamEvent(IEnumerable<GeminiCandidate>? Candidates);

    // === DTOs for Gemini JSON ===
    private record GeminiPart([property: JsonPropertyName("text")] string Text);
    private record GeminiContent(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("parts")] IEnumerable<GeminiPart> Parts);

    private record GeminiSystemInstruction(
        [property: JsonPropertyName("parts")] IEnumerable<GeminiPart> Parts);

    private record GeminiGenerateContentRequest(
        [property: JsonPropertyName("contents")] IEnumerable<GeminiContent> Contents,
        // Google Generative Language API expects camelCase 'systemInstruction'
        [property: JsonPropertyName("systemInstruction")] GeminiSystemInstruction? SystemInstruction);

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
