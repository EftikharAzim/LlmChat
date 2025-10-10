namespace LlmChat.Tools.Google;

public class DiagnosticResult
{
    public bool OverallSuccess { get; set; }
    public bool CredentialsFound { get; set; }
    public bool OAuthSuccessful { get; set; }
    public bool ApiAccessSuccessful { get; set; }
    public string? ErrorMessage { get; set; }

    public override string ToString()
    {
        if (OverallSuccess)
            return "? Google Calendar API is working correctly!";

        var status = "? Google Calendar API Diagnostics:\n";
        status += $"  Credentials Found: {(CredentialsFound ? "?" : "?")}\n";
        status += $"  OAuth Successful: {(OAuthSuccessful ? "?" : "?")}\n";
        status += $"  API Access: {(ApiAccessSuccessful ? "?" : "?")}\n";
        
        if (!string.IsNullOrEmpty(ErrorMessage))
            status += $"  Error: {ErrorMessage}\n";

        return status;
    }
}