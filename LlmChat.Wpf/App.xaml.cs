using LlmChat.Abstractions;
using LlmChat.Providers.Gemini;
using LlmChat.Wpf.ViewModels;
using LlmChat.Wpf.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

                    services.AddSingleton<ChatViewModel>();
                    services.AddSingleton<ChatWindow>();
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
