using LlmChat.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

var builder = Host.CreateApplicationBuilder(args);
// Ensure developer user-secrets are loaded when present
builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Services.AddLogging();
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var provider = config["LlmProvider"]?.ToLowerInvariant() ?? "gemini";
    var model = config[$"Providers:{provider}:Model"];
    switch (provider)
    {
        case "gemini":
            // Try configuration first, then fall back to environment variable for convenience.
            var geminiKey = config["Providers:Gemini:ApiKey"]
                          ?? config["GEMINI_API_KEY"]
                          ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (string.IsNullOrWhiteSpace(geminiKey))
                throw new InvalidOperationException("Gemini API key not set. Provide it via appsettings.json (Providers:Gemini:ApiKey), the GEMINI_API_KEY environment variable, or `dotnet user-secrets` during development.");
            return new LlmChat.Providers.Gemini.GeminiChatClient(new HttpClient(), geminiKey, model);
        default:
            throw new InvalidOperationException($"Unknown LlmProvider '{provider}'. Supported: 'gemini'.");
    }
});

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LlmChat.Console");
var configMain = host.Services.GetRequiredService<IConfiguration>();
var providerName = configMain["LlmProvider"] ?? "gemini";
var client = host.Services.GetRequiredService<IChatClient>();

logger.LogInformation("LLM Chat ({Provider}). Type '/exit' to quit.", providerName);
Console.WriteLine($"LLM Chat ({providerName}). Type '/exit' to quit.\n");
var history = new List<ChatMessage>();

while (true)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("You > ");
    Console.ResetColor();

    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) continue;
    if (input.Trim().Equals("/exit", StringComparison.OrdinalIgnoreCase)) break;

    history.Add(new ChatMessage(ChatRole.User, input));

    var req = new ChatRequest(history, MaxTokens: 512);
    try
    {
        var res = await client.CompleteAsync(req);
        history.Add(new ChatMessage(ChatRole.Assistant, res.Text));

        // logger.LogInformation("Bot > {Text}", res.Text);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Bot > {res.Text}\n");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during chat completion");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {ex.Message}");
        Console.ResetColor();
    }
}
