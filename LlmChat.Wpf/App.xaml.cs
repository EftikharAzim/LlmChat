using LlmChat.Abstractions;
using LlmChat.Agent;
using LlmChat.Agent.Planning;
using LlmChat.Memory;
using LlmChat.Providers.Gemini;
using LlmChat.Tools;
using LlmChat.Tools.Google;
using LlmChat.Wpf.ViewModels;
using LlmChat.Wpf.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Windows;

namespace LlmChat.Wpf
{
    public partial class App : Application
    {
        private IHost _host = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((ctx, config) =>
                {
                    // Load config from the application base directory (bin output).
                    // This avoids fragile relative ".." jumps and works in publish scenarios.
                    var baseDir = AppContext.BaseDirectory;
                    config.SetBasePath(baseDir);
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    config.AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                    // User secrets (Development) + environment variables are supported
                    config.AddUserSecrets<App>(optional: true);
                    config.AddEnvironmentVariables();
                })

                .ConfigureServices((ctx, services) =>
                {
                    var cfg = ctx.Configuration;
                    // Prefer hierarchical config value; fall back to common env var names.
                    var apiKey = cfg["Providers:Gemini:ApiKey"]
                               ?? cfg["GEMINI_API_KEY"]
                               ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
                    var model = cfg["Providers:Gemini:Model"] ?? "gemini-2.0-flash";
                    var system = cfg["Providers:Gemini:SystemPrompt"];

                    services.AddSingleton<IChatClient>(sp =>
                    {
                        if (string.IsNullOrWhiteSpace(apiKey))
                            return new Services.FallbackChatClient("Missing Gemini API key. Configure Providers:Gemini:ApiKey.");

                        var http = new HttpClient
                        {
                            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
                        };
                        return new GeminiChatClient(http, apiKey, model, system);
                    });

                    // Create models dictionary for future expansion
                    services.AddSingleton(sp =>
                    {
                        var chatClients = new Dictionary<string, IChatClient>();
                        chatClients["Gemini"] = sp.GetRequiredService<IChatClient>();
                        // Future models can be added here
                        return chatClients;
                    });

                    services.AddSingleton<ChatViewModel>();
                    
                    services.AddSingleton<ChatWindow>();
                    services.AddSingleton<IMemoryStore, InMemoryStore>();

                    // Register Google Calendar Service with proper dependency injection
                    services.AddSingleton<IGoogleCalendarService>(sp =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var logger = sp.GetService<ILogger<GoogleCalendarService>>();
                        return new GoogleCalendarService(config, logger);
                    });
                    services.AddSingleton<ITool>(sp =>
                    {
                        var calendarService = sp.GetRequiredService<IGoogleCalendarService>();
                        var logger = sp.GetService<ILogger<GoogleCalendarSearchTool>>();
                        return new GoogleCalendarSearchTool(calendarService, logger);
                    });
                    services.AddSingleton<IToolRegistry, ToolRegistry>();

                    services.AddSingleton<IIntentRouter, LlmIntentRouter>();
                    services.AddSingleton<IAgent>(sp =>
                    {
                        var llm = sp.GetRequiredService<IChatClient>();
                        var router = sp.GetRequiredService<IIntentRouter>();
                        var memory = sp.GetRequiredService<IMemoryStore>();
                        var tools = sp.GetRequiredService<IToolRegistry>();
                        var logger = sp.GetService<ILogger<AgentRuntime>>();
                        return new AgentRuntime(llm, router, memory, tools, logger);
                    });
                })
                .Build();

            var window = _host.Services.GetRequiredService<ChatWindow>();
            window.Show();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            try { await _host.StopAsync(TimeSpan.FromSeconds(2)); }
            finally { _host.Dispose(); }
            base.OnExit(e);
        }
    }
}
