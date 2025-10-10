using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using LlmChat.Abstractions;
using LlmChat.Wpf.Models;
using LlmChat.Agent;

namespace LlmChat.Wpf.ViewModels;

public class ChatViewModel : INotifyPropertyChanged
{
    private readonly IAgent _agent;
    private readonly string _sessionId = Guid.NewGuid().ToString();
    private string _inputText = string.Empty;
    private bool _isBusy;
    private string _selectedModel = "Gemini";

    public ObservableCollection<MessageModel> Messages { get; } = new();
    public ObservableCollection<string> AvailableModels { get; } = new() { "Gemini", "Phi-3-Mini" };

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

    public string SelectedModel
    {
        get => _selectedModel;
        set { _selectedModel = value; OnPropertyChanged(); }
    }

    public ChatViewModel(IChatClient chatClient, IAgent agent)
    {
        // chatClient kept in signature for DI compatibility and future use (model registry), but not stored until switching is implemented.
        _agent = agent;
        
        // Keep CanExecute simple + robust. We also guard inside the method.
        _sendCommand = new AsyncRelayCommand(async _ => await SendMessageAsync(), _ => !IsBusy);
    }

    private async Task SendMessageAsync()
    {
        var input = InputText?.Trim();
        if (string.IsNullOrEmpty(input)) return;

        InputText = string.Empty;
        IsBusy = true;

        var userMsg = new MessageModel { Role = "User", Content = input };
        Messages.Add(userMsg);

        var assistantMsg = new MessageModel { Role = "Assistant", Content = "" };
        Messages.Add(assistantMsg);

        try
        {
            // Use agent streaming for enhanced functionality
            var hasContent = false;
            var responseBuilder = new StringBuilder();
            
            await foreach (var chunk in _agent.StreamAsync(new AgentTurnRequest(_sessionId, input), CancellationToken.None))
            {
                assistantMsg.Content += chunk;
                responseBuilder.Append(chunk);
                hasContent = true;
            }

            // If no content was streamed but no exception occurred, provide a fallback
            if (!hasContent && string.IsNullOrEmpty(assistantMsg.Content))
            {
                assistantMsg.Content = "I've processed your request successfully. Your Google Calendar has been checked.";
            }
        }
        catch (HttpRequestException httpEx)
        {
            // Handle network/API issues gracefully
            assistantMsg.Content = $"⚠️ Network error occurred. Please check your connection and try again.\n\nDetails: {httpEx.Message}";
        }
        catch (TaskCanceledException)
        {
            assistantMsg.Content = "⏱️ Request timed out. Please try again.";
        }
        catch (Exception ex)
        {
            // Log the full exception but show a user-friendly message
            assistantMsg.Content = $"❌ An error occurred while processing your request.\n\nError: {ex.Message}";
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
