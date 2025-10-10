using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LlmChat.Tools.Google;

public sealed class GoogleCalendarService : IGoogleCalendarService
{
    private readonly global::Google.Apis.Calendar.v3.CalendarService _svc;
    private readonly ILogger<GoogleCalendarService>? _logger;

    public GoogleCalendarService(IConfiguration config, ILogger<GoogleCalendarService>? logger = null)
    {
        _logger = logger;

        // Try to get credentials from user secrets first
        var clientId = config["Google:ClientId"];
        var clientSecret = config["Google:ClientSecret"];

        _logger?.LogInformation("Looking for Google credentials...");

        // If not found in user secrets, try to load from credentials.json file
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            _logger?.LogInformation("Google credentials not found in configuration, attempting to load from credentials.json file...");

            try
            {
                var credentialsPath = Path.Combine(AppContext.BaseDirectory, "credentials.json");
                if (!File.Exists(credentialsPath))
                {
                    // Try in the project directory if not in output directory
                    credentialsPath = Path.Combine(Directory.GetCurrentDirectory(), "credentials.json");
                }

                if (File.Exists(credentialsPath))
                {
                    var credentialsJson = File.ReadAllText(credentialsPath);
                    var credentialsDoc = JsonDocument.Parse(credentialsJson);

                    if (credentialsDoc.RootElement.TryGetProperty("installed", out var installed))
                    {
                        if (installed.TryGetProperty("client_id", out var idElement))
                            clientId = idElement.GetString();
                        if (installed.TryGetProperty("client_secret", out var secretElement))
                            clientSecret = secretElement.GetString();

                        _logger?.LogInformation("Successfully loaded Google credentials from credentials.json file.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load credentials from credentials.json file.");
            }
        }
        else
        {
            _logger?.LogInformation("Found Google credentials in configuration.");
        }

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            var errorMessage = "Google Calendar credentials not found. Please configure them using one of these methods:\n" +
                             "1. User Secrets: dotnet user-secrets set \"Google:ClientId\" \"your-client-id\"\n" +
                             "2. User Secrets: dotnet user-secrets set \"Google:ClientSecret\" \"your-client-secret\"\n" +
                             "3. Place a credentials.json file in the application directory\n" +
                             "4. Set environment variables GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET";

            _logger?.LogError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        _logger?.LogInformation("Initializing Google Calendar service with OAuth flow...");
        _logger?.LogDebug("Client ID: {ClientId}", clientId?.Substring(0, Math.Min(10, clientId.Length)) + "...");

        try
        {
            // Create credentials via desktop OAuth flow (first run pops a browser)
            var credential = global::Google.Apis.Auth.OAuth2.GoogleWebAuthorizationBroker.AuthorizeAsync(
                new global::Google.Apis.Auth.OAuth2.ClientSecrets
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                },
                new[] { global::Google.Apis.Calendar.v3.CalendarService.Scope.CalendarReadonly },
                "user",
                CancellationToken.None,
                new global::Google.Apis.Util.Store.FileDataStore("LlmChat.GoogleAuth", true)
            ).GetAwaiter().GetResult();

            _logger?.LogInformation("OAuth credential obtained successfully.");

            _svc = new global::Google.Apis.Calendar.v3.CalendarService(new global::Google.Apis.Services.BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "LlmChat Agent"
            });

            _logger?.LogInformation("Google Calendar service initialized successfully.");

            // Test the connection immediately
            _ = Task.Run(async () => await TestConnectionAsync());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Google Calendar service. Common issues:\n" +
                "1. OAuth consent screen not configured\n" +
                "2. Google Calendar API not enabled\n" +
                "3. Invalid client credentials\n" +
                "4. Redirect URI not configured properly");
            throw;
        }
    }

    private async Task TestConnectionAsync()
    {
        try
        {
            _logger?.LogInformation("Testing Google Calendar API connection...");
            
            // Test with a simple calendar list request first
            var calendarsRequest = _svc.CalendarList.List();
            calendarsRequest.MaxResults = 1;
            var calendarsResponse = await calendarsRequest.ExecuteAsync();
            
            _logger?.LogInformation("Calendar API connection test successful. Found {Count} calendars.", 
                calendarsResponse.Items?.Count ?? 0);

            // Test a simple events list request
            _logger?.LogInformation("Testing events list API...");
            var eventsRequest = _svc.Events.List("primary");
            eventsRequest.MaxResults = 1;
            eventsRequest.SingleEvents = true;
            eventsRequest.OrderBy = global::Google.Apis.Calendar.v3.EventsResource.ListRequest.OrderByEnum.StartTime;
            eventsRequest.TimeMinDateTimeOffset = DateTime.Now.AddDays(-7);
            eventsRequest.TimeMaxDateTimeOffset = DateTime.Now.AddDays(7);
            
            var eventsResponse = await eventsRequest.ExecuteAsync();
            _logger?.LogInformation("Events API test successful. Found {Count} events.", 
                eventsResponse.Items?.Count ?? 0);
        }
        catch (global::Google.GoogleApiException googleEx)
        {
            _logger?.LogError(googleEx, "Calendar API connection test failed. Status: {Status}, Message: {Message}, Details: {Details}", 
                googleEx.HttpStatusCode, googleEx.Message, googleEx.Error?.ToString());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during Calendar API connection test.");
        }
    }

    public async Task<IReadOnlyList<global::Google.Apis.Calendar.v3.Data.Event>> SearchAsync(
        string? query, DateTime? start, DateTime? end, int max, CancellationToken ct)
    {
        try
        {
            _logger?.LogInformation("Searching Google Calendar - Query: '{Query}', Start: {Start}, End: {End}, Max: {Max}", 
                query, start, end, max);

            // Validate and set reasonable defaults
            var actualMax = Math.Min(Math.Max(max, 1), 2500); // Google Calendar API max is 2500
            var actualStart = start ?? DateTime.Now.AddDays(-30);
            var actualEnd = end ?? DateTime.Now.AddDays(30);

            // Ensure start is before end
            if (actualStart >= actualEnd)
            {
                actualEnd = actualStart.AddDays(1);
                _logger?.LogWarning("Start date was after end date, adjusted end date to {AdjustedEnd}", actualEnd);
            }

            _logger?.LogDebug("Executing Google Calendar API request for calendar: primary with adjusted parameters - Start: {Start}, End: {End}, Max: {Max}",
                actualStart, actualEnd, actualMax);

            var req = _svc.Events.List("primary");
            
            // Only add query if it's not empty
            if (!string.IsNullOrWhiteSpace(query))
            {
                req.Q = query.Trim();
                _logger?.LogDebug("Added search query: '{Query}'", req.Q);
            }

            req.TimeMinDateTimeOffset = actualStart;
            req.TimeMaxDateTimeOffset = actualEnd;
            req.SingleEvents = true;
            req.OrderBy = global::Google.Apis.Calendar.v3.EventsResource.ListRequest.OrderByEnum.StartTime;
            req.MaxResults = actualMax;

            var resp = await req.ExecuteAsync(ct);
            var events = (resp.Items ?? new List<global::Google.Apis.Calendar.v3.Data.Event>()).ToList();
            
            _logger?.LogInformation("Successfully retrieved {Count} calendar events.", events.Count);
            return events;
        }
        catch (System.Net.Http.HttpRequestException httpEx)
        {
            _logger?.LogError(httpEx, "HTTP request failed when calling Google Calendar API. Message: {Message}", httpEx.Message);
            
            var detailedMessage = $"Network error occurred while accessing Google Calendar API:\n" +
                $"Error: {httpEx.Message}\n\n" +
                "This could be caused by:\n" +
                "1. Network connectivity issues\n" +
                "2. Google API service temporarily unavailable\n" +
                "3. Request timeout\n" +
                "4. Firewall or proxy blocking the request\n" +
                "5. Invalid request parameters causing server rejection\n\n" +
                "Please check your internet connection and try again.";
            
            throw new InvalidOperationException(detailedMessage, httpEx);
        }
        catch (global::Google.GoogleApiException googleEx)
        {
            _logger?.LogError(googleEx, "Google API error occurred. Status: {Status}, Message: {Message}, Error Details: {ErrorDetails}", 
                googleEx.HttpStatusCode, googleEx.Message, 
                googleEx.Error != null ? System.Text.Json.JsonSerializer.Serialize(googleEx.Error) : "No detailed error info");

            var detailedMessage = BuildDetailedErrorMessage(googleEx);
            throw new InvalidOperationException(detailedMessage, googleEx);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error occurred while searching Google Calendar.");
            throw;
        }
    }

    private string BuildDetailedErrorMessage(global::Google.GoogleApiException googleEx)
    {
        var message = $"Google Calendar API Error ({googleEx.HttpStatusCode}):\n";
        
        if (googleEx.HttpStatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            message += "400 Bad Request - Common causes:\n" +
                "1. Google Calendar API is not enabled in Google Cloud Console\n" +
                "2. OAuth consent screen is not configured properly\n" +
                "3. Invalid request parameters\n" +
                "4. Missing or invalid authentication scopes\n" +
                "5. Project quotas exceeded\n\n";
        }
        else if (googleEx.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            message += "401 Unauthorized - Authentication issues:\n" +
                "1. Invalid or expired OAuth token\n" +
                "2. OAuth consent not granted\n" +
                "3. Invalid client credentials\n\n";
        }
        else if (googleEx.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            message += "403 Forbidden - Permission issues:\n" +
                "1. Calendar API not enabled\n" +
                "2. Insufficient OAuth scopes\n" +
                "3. Access to resource denied\n\n";
        }

        message += $"Detailed Google Error: {googleEx.Message}\n";
        
        if (googleEx.Error != null)
        {
            message += $"Error Code: {googleEx.Error.Code}\n";
            message += $"Error Message: {googleEx.Error.Message}\n";
            
            if (googleEx.Error.Errors != null && googleEx.Error.Errors.Any())
            {
                message += "Specific Errors:\n";
                foreach (var error in googleEx.Error.Errors)
                {
                    message += $"  - {error.Domain}: {error.Reason} - {error.Message}\n";
                }
            }
        }

        return message;
    }
}
