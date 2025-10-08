using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LlmChat.Abstractions;
using LlmChat.Wpf.Models;

namespace LlmChat.Wpf.ViewModels;

public class ChatViewModel : INotifyPropertyChanged
{
    private readonly IChatClient _chatClient;
    private string _inputText = string.Empty;
    private bool _isBusy;

    public ObservableCollection<MessageModel> Messages { get; } = new();

    private readonly AsyncRelayCommand _sendCommand;
    public ICommand SendCommand => _sendCommand;

    public string InputText
    {
        get => _inputText;
        set { _inputText = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    public ChatViewModel(IChatClient chatClient)
    {
        _chatClient = chatClient;
        // Keep CanExecute simple + robust. We also guard inside the method.
        _sendCommand = new AsyncRelayCommand(async _ => await SendMessageAsync(), _ => !IsBusy);
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;

        var userText = InputText;
        InputText = string.Empty;

        Messages.Add(new MessageModel { Role = "User", Content = userText });
        IsBusy = true;

        try
        {
            // Preserve full session context
            var chatHistory = Messages
                .Select(m => new ChatMessage(
                    m.Role == "User" ? ChatRole.User :
                    m.Role == "Assistant" ? ChatRole.Assistant : ChatRole.System,
                    m.Content))
                .ToList();

            var response = await _chatClient.CompleteAsync(new ChatRequest(chatHistory));
            var text = string.IsNullOrWhiteSpace(response.Text) ? "[No response]" : response.Text;

            Messages.Add(new MessageModel { Role = "Assistant", Content = text });
        }
        catch (Exception ex)
        {
            Messages.Add(new MessageModel { Role = "System", Content = $"[Error] {ex.Message}" });
        }
        finally
        {
            IsBusy = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
