namespace LlmChat.Wpf.Models;

public class MessageModel
{
    public string Role { get; set; } = string.Empty;   // "User", "Assistant", "System"
    public string Content { get; set; } = string.Empty;
}
