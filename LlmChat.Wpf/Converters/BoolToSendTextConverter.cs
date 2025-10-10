using System;
using System.Globalization;
using System.Windows.Data;

namespace LlmChat.Wpf.Converters;

public class BoolToSendTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isBusy = value is bool b && b;
        return isBusy ? "?" : "Send";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
