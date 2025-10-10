using LlmChat.Tools.Google;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// Google Calendar Diagnostics and Test Tool
var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

var logger = loggerFactory.CreateLogger<GoogleCalendarDiagnostics>();

Console.WriteLine("?? Google Calendar API Diagnostics Tool");
Console.WriteLine("=====================================");

// Run basic diagnostics first
var diagnostics = new GoogleCalendarDiagnostics(logger);
var result = await diagnostics.RunDiagnosticsAsync(config);

Console.WriteLine("\n" + result.ToString());

if (!result.OverallSuccess)
{
    Console.WriteLine("\n?? For detailed troubleshooting steps, see:");
    Console.WriteLine("   LlmChat.Tools.Google/TROUBLESHOOTING.md");
    Environment.Exit(1);
}

// If diagnostics pass, run specific search tests
Console.WriteLine("\n?? Running specific search API tests...");
Console.WriteLine("======================================");

try
{
    var serviceLogger = loggerFactory.CreateLogger<GoogleCalendarService>();
    var service = new GoogleCalendarService(config, serviceLogger);
    
    Console.WriteLine("\n1?? Testing simple search (no parameters)...");
    var events1 = await service.SearchAsync(null, null, null, 5, CancellationToken.None);
    Console.WriteLine($"   ? Found {events1.Count} events");

    Console.WriteLine("\n2?? Testing search with date range...");
    var start = DateTime.Now.AddDays(-7);
    var end = DateTime.Now.AddDays(7);
    var events2 = await service.SearchAsync(null, start, end, 5, CancellationToken.None);
    Console.WriteLine($"   ? Found {events2.Count} events from {start:yyyy-MM-dd} to {end:yyyy-MM-dd}");

    Console.WriteLine("\n3?? Testing search with query...");
    var events3 = await service.SearchAsync("meeting", start, end, 5, CancellationToken.None);
    Console.WriteLine($"   ? Found {events3.Count} events matching 'meeting'");

    Console.WriteLine("\n?? All tests passed! Google Calendar API is working correctly.");
    
    if (events2.Any())
    {
        Console.WriteLine("\n?? Sample events:");
        foreach (var evt in events2.Take(3))
        {
            Console.WriteLine($"   • {evt.Summary} ({evt.Start?.DateTimeDateTimeOffset?.ToString("yyyy-MM-dd HH:mm") ?? evt.Start?.Date})");
        }
    }
    
    Console.WriteLine("\n? Your Google Calendar integration is ready to use!");
}
catch (Exception ex)
{
    Console.WriteLine($"\n? Search test failed: {ex.Message}");
    Console.WriteLine($"\nThis suggests the issue is with the specific search functionality.");
    Console.WriteLine($"Full error details:\n{ex}");
    Environment.Exit(1);
}

Environment.Exit(0);