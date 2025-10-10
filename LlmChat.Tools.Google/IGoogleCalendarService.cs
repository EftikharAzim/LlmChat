namespace LlmChat.Tools.Google;

public interface IGoogleCalendarService
{
    Task<IReadOnlyList<global::Google.Apis.Calendar.v3.Data.Event>> SearchAsync(
        string? query, DateTime? start, DateTime? end, int max, CancellationToken ct);
}
