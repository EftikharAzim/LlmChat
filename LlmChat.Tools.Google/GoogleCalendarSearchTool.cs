using Microsoft.Extensions.Logging;

namespace LlmChat.Tools.Google;

public sealed class GoogleCalendarSearchTool : ITool
{
    public string Name => "google.calendar.search";
    public string Description => "Search primary calendar events by text and time window.";
    public string InputSchemaJson => """{""type"":""object"",""properties"":{""query"":{""type"":""string""},""start"":{""type"":""string"",""format"":""date-time""},""end"":{""type"":""string"",""format"":""date-time""},""max"":{""type"":""integer"",""minimum"":1,""maximum"":50}},""additionalProperties"":false}""";
    public string OutputSchemaJson => """{""type"":""array"",""items"":{""type"":""object"",""properties"":{""title"":{""type"":""string""},""start"":{""type"":""string""},""end"":{""type"":""string""},""location"":{""type"":""string""},""link"":{""type"":""string""}}}}""";

    private readonly IGoogleCalendarService _svc;
    private readonly ILogger<GoogleCalendarSearchTool>? _logger;

    public GoogleCalendarSearchTool(IGoogleCalendarService svc, ILogger<GoogleCalendarSearchTool>? logger = null)
    {
        _svc = svc;
        _logger = logger;
    }

    public async Task<ToolResult> InvokeAsync(string sessionId, IReadOnlyDictionary<string, object> args, CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Google Calendar search tool invoked with parameters: {Args}",
                string.Join(", ", args.Select(kvp => $"{kvp.Key}={kvp.Value}")));

            // Parse and validate parameters
            string? q = null;
            DateTime? start = null;
            DateTime? end = null;
            int max = 10;

            if (args.TryGetValue("query", out var _q)) q = _q?.ToString();

            if (args.TryGetValue("start", out var _s))
            {
                var startStr = _s?.ToString();
                if (!string.IsNullOrEmpty(startStr) && DateTime.TryParse(startStr, out var sdt)) start = sdt;
            }

            if (args.TryGetValue("end", out var _e))
            {
                var endStr = _e?.ToString();
                if (!string.IsNullOrEmpty(endStr) && DateTime.TryParse(endStr, out var edt)) end = edt;
            }

            if (args.TryGetValue("max", out var _m))
            {
                var maxStr = _m?.ToString();
                if (!string.IsNullOrEmpty(maxStr) && int.TryParse(maxStr, out var mi) && mi >= 1 && mi <= 50) max = mi;
            }

            var events = await _svc.SearchAsync(q, start, end, max, ct);

            var simplified = events.Select(ev => new
            {
                title = ev.Summary,
                start = ev.Start?.DateTimeDateTimeOffset?.ToString("yyyy-MM-dd HH:mm") ?? ev.Start?.Date,
                end = ev.End?.DateTimeDateTimeOffset?.ToString("yyyy-MM-dd HH:mm") ?? ev.End?.Date,
                location = ev.Location,
                link = ev.HtmlLink
            }).ToList();

            // Build a concise human-readble summary for console clarity
            string human;
            if (simplified.Count == 0)
            {
                human = "No calendar events found" +
                        (start.HasValue && end.HasValue ? $" between {start:yyyy-MM-dd} and {end:yyyy-MM-dd}" :
                         start.HasValue ? $" from {start:yyyy-MM-dd}" :
                         end.HasValue ? $" until {end:yyyy-MM-dd}" : "") +
                        (!string.IsNullOrWhiteSpace(q) ? $" matching '{q}'." : ".");
            }
            else
            {
                var header = $"Found {simplified.Count} event(s)" +
                             (start.HasValue && end.HasValue ? $" between {start:yyyy-MM-dd} and {end:yyyy-MM-dd}" : "") +
                             (!string.IsNullOrWhiteSpace(q) ? $" matching '{q}'" : "") + ":\n";
                var lines = simplified.Take(5)
                    .Select(ev => $"- {ev.title} ({ev.start}{(ev.end is not null ? $" - {ev.end}" : string.Empty)})");
                human = header + string.Join("\n", lines);
                if (simplified.Count > 5) human += $"\n…and {simplified.Count - 5} more";
            }

            var payload = new { message = human, events = simplified };
            _logger?.LogDebug("Calendar tool returning: {Human}", human);
            return new ToolResult(true, payload);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("400 Bad Request") || ex.Message.Contains("Bad Request"))
        {
            _logger?.LogError(ex, "Google Calendar API returned Bad Request error.");
            var userFriendlyMessage = "Google Calendar API returned a Bad Request error. " +
                "Since your Google Calendar setup is working correctly (as verified by diagnostics), " +
                "this might be caused by invalid parameters passed to the search. " +
                $"Error details: {ex.Message}";

            return new ToolResult(false, null, userFriendlyMessage);
        }
        catch (System.Net.Http.HttpRequestException httpEx)
        {
            _logger?.LogError(httpEx, "Network error occurred during Google Calendar search.");
            var userFriendlyMessage = "Network error occurred while accessing Google Calendar. " +
                "Please check your internet connection and try again. " +
                $"Error: {httpEx.Message}";

            return new ToolResult(false, null, userFriendlyMessage);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error occurred during Google Calendar search.");
            return new ToolResult(false, null, $"Failed to search Google Calendar: {ex.Message}");
        }
    }
}
