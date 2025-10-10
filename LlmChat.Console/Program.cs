using LlmChat.Abstractions;
using LlmChat.Agent;
using LlmChat.Agent.Planning;
using LlmChat.Memory;
using LlmChat.Tools;
using LlmChat.Tools.Google;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

var builder = Host.CreateApplicationBuilder(args);

// Ensure developer user-secrets are loaded when present
builder.Configuration.AddUserSecrets<Program>(optional: true);

// Configure logging: keep console clean by default
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
});
// Default to Warning; override via Logging config or env vars if needed
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// Configure LLM Client
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var provider = config["LlmProvider"]?.ToLowerInvariant() ?? "gemini";
    var model = config[$"Providers:{provider}:Model"];
    switch (provider)
    {
        case "gemini":
            var geminiKey = config["Providers:Gemini:ApiKey"]
                          ?? config["GEMINI_API_KEY"]
                          ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            var system = config["Providers:Gemini:SystemPrompt"]; // align with WPF for consistent behavior
            if (string.IsNullOrWhiteSpace(geminiKey))
                throw new InvalidOperationException("Gemini API key not set. Configure via user secrets: dotnet user-secrets set \"Providers:Gemini:ApiKey\" \"your-key\"");
            return new LlmChat.Providers.Gemini.GeminiChatClient(new HttpClient(), geminiKey, model, system);
        default:
            throw new InvalidOperationException($"Unknown LlmProvider '{provider}'. Supported: 'gemini'.");
    }
});

// Configure Agent services
builder.Services.AddSingleton<IMemoryStore, InMemoryStore>();

// Configure Google Calendar Tool
builder.Services.AddSingleton<IGoogleCalendarService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetService<ILogger<GoogleCalendarService>>();
    return new GoogleCalendarService(config, logger);
});

builder.Services.AddSingleton<ITool>(sp =>
{
    var calendarService = sp.GetRequiredService<IGoogleCalendarService>();
    var logger = sp.GetService<ILogger<GoogleCalendarSearchTool>>();
    return new GoogleCalendarSearchTool(calendarService, logger);
});

builder.Services.AddSingleton<IToolRegistry, ToolRegistry>();
builder.Services.AddSingleton<IIntentRouter, LlmIntentRouter>();
builder.Services.AddSingleton<IAgent>(sp =>
{
    var llm = sp.GetRequiredService<IChatClient>();
    var router = sp.GetRequiredService<IIntentRouter>();
    var memory = sp.GetRequiredService<IMemoryStore>();
    var tools = sp.GetRequiredService<IToolRegistry>();
    var logger = sp.GetService<ILogger<AgentRuntime>>();
    return new AgentRuntime(llm, router, memory, tools, logger);
});

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LlmChat.Console");
var config = host.Services.GetRequiredService<IConfiguration>();
var providerName = config["LlmProvider"] ?? "gemini";
var agent = host.Services.GetRequiredService<IAgent>();

Console.WriteLine($"🤖 LLM Chat Console with Google Calendar ({providerName})");
Console.WriteLine("===============================================");
Console.WriteLine("Features:");
Console.WriteLine("  • Natural language chat");
Console.WriteLine("  • Google Calendar integration");
Console.WriteLine("  • Ask about your calendar events");
Console.WriteLine();
Console.WriteLine("Examples:");
Console.WriteLine("  'What's on my calendar today?'");
Console.WriteLine("  'Do I have any meetings this week?'");
Console.WriteLine("  'Show me my upcoming events'");
Console.WriteLine();
Console.WriteLine("Type '/exit' to quit");
Console.WriteLine();

var sessionId = Guid.NewGuid().ToString();

try
{
    // Test Google Calendar connection
    var calendarService = host.Services.GetService<IGoogleCalendarService>();
    if (calendarService != null)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✅ Google Calendar connected successfully!");
        Console.ResetColor();
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"⚠️  Google Calendar not configured: {ex.Message}");
    Console.WriteLine("   Calendar features will not be available.");
    Console.ResetColor();
}

Console.WriteLine();

while (true)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("You > ");
    Console.ResetColor();

    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) continue;
    
    if (input.Trim().Equals("/exit", StringComparison.OrdinalIgnoreCase)) break;

    try
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("Bot > ");
        Console.ResetColor();

        // Use agent with tools
        var agentRequest = new AgentTurnRequest(sessionId, input);
        await foreach (var chunk in agent.StreamAsync(agentRequest))
        {
            Console.Write(chunk);
        }
        Console.WriteLine("\n");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during chat");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {ex.Message}");
        Console.ResetColor();
        Console.WriteLine();
    }
}
