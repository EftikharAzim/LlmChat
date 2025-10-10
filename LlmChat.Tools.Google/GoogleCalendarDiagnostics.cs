using LlmChat.Tools.Google;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LlmChat.Tools.Google;

/// <summary>
/// Diagnostic tool to test Google Calendar API connectivity
/// </summary>
public class GoogleCalendarDiagnostics
{
    private readonly ILogger<GoogleCalendarDiagnostics>? _logger;

    public GoogleCalendarDiagnostics(ILogger<GoogleCalendarDiagnostics>? logger = null)
    {
        _logger = logger;
    }

    public async Task<DiagnosticResult> RunDiagnosticsAsync(IConfiguration config)
    {
        var result = new DiagnosticResult();
        
        try
        {
            _logger?.LogInformation("Starting Google Calendar API diagnostics...");
            
            // Step 1: Check credentials
            result.CredentialsFound = CheckCredentials(config);
            if (!result.CredentialsFound)
            {
                result.ErrorMessage = "Google Calendar credentials not found in configuration.";
                return result;
            }

            // Step 2: Test OAuth flow
            result.OAuthSuccessful = await TestOAuthFlowAsync(config);
            if (!result.OAuthSuccessful)
            {
                result.ErrorMessage = "OAuth authentication failed.";
                return result;
            }

            // Step 3: Test API access
            var (apiSuccess, apiError) = await TestApiAccessAsync(config);
            result.ApiAccessSuccessful = apiSuccess;
            if (!apiSuccess)
            {
                result.ErrorMessage = apiError;
                return result;
            }

            result.OverallSuccess = true;
            _logger?.LogInformation("Google Calendar API diagnostics completed successfully!");
            
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Diagnostic error: {ex.Message}";
            _logger?.LogError(ex, "Error during Google Calendar diagnostics");
        }

        return result;
    }

    private bool CheckCredentials(IConfiguration config)
    {
        var clientId = config["Google:ClientId"];
        var clientSecret = config["Google:ClientSecret"];
        
        var found = !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret);
        _logger?.LogInformation("Credentials check: {Status}", found ? "Found" : "Not Found");
        
        return found;
    }

    private async Task<bool> TestOAuthFlowAsync(IConfiguration config)
    {
        try
        {
            _logger?.LogInformation("Testing OAuth flow...");
            
            var clientId = config["Google:ClientId"];
            var clientSecret = config["Google:ClientSecret"];

            var credential = await global::Google.Apis.Auth.OAuth2.GoogleWebAuthorizationBroker.AuthorizeAsync(
                new global::Google.Apis.Auth.OAuth2.ClientSecrets
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                },
                new[] { global::Google.Apis.Calendar.v3.CalendarService.Scope.CalendarReadonly },
                "user",
                CancellationToken.None,
                new global::Google.Apis.Util.Store.FileDataStore("LlmChat.GoogleAuth.Diagnostics", true)
            );

            _logger?.LogInformation("OAuth flow successful. Token: {HasToken}", credential.Token != null);
            return credential != null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "OAuth flow failed");
            return false;
        }
    }

    private async Task<(bool success, string? error)> TestApiAccessAsync(IConfiguration config)
    {
        try
        {
            _logger?.LogInformation("Testing Google Calendar API access...");
            
            var service = new GoogleCalendarService(config, _logger as ILogger<GoogleCalendarService>);
            
            // Try to get a simple list of events (max 1 to minimize impact)
            var events = await service.SearchAsync(null, DateTime.Now.AddDays(-7), DateTime.Now.AddDays(7), 1, CancellationToken.None);
            
            _logger?.LogInformation("API access test successful. Retrieved {Count} events.", events.Count);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "API access test failed");
            return (false, ex.Message);
        }
    }
}
