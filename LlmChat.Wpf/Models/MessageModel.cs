using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LlmChat.Wpf.Models;

public class MessageModel : INotifyPropertyChanged
{
    private string _role = string.Empty;
    private string _content = string.Empty;

    public string Role 
    { 
        get => _role;
        set 
        { 
            _role = value; 
            OnPropertyChanged(); 
        }
    }

    public string Content 
    { 
        get => _content;
        set 
        { 
            _content = value; 
            OnPropertyChanged(); 
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
