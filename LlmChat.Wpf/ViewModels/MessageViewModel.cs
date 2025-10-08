namespace LlmChat.Wpf.ViewModels
{
    public class MessageViewModel
    {
        public string Role { get; }
        public string Content { get; }

        public MessageViewModel(string role, string content)
        {
            Role = role;
            Content = content;
        }

        public bool IsUser => Role == "User";
    }
}
