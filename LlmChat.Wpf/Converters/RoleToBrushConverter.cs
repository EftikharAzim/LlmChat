using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LlmChat.Wpf.Converters;

public class RoleToBrushConverter : IValueConverter
{
    private static readonly Brush User = (Brush)App.Current.Resources["UserBubble"];
    private static readonly Brush Bot = (Brush)App.Current.Resources["BotBubble"];
    private static readonly Brush System = new SolidColorBrush(Color.FromRgb(120, 53, 15)); // amber-900-ish

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var role = value?.ToString();
        return role switch
        {
            "User" => User,
            "Assistant" => Bot,
            "System" => System,
            _ => Bot
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
