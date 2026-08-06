using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Meowgnal.Views;

public sealed class SignalTypeToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var type = value as string ?? "";
        return type == "buy"
            ? new SolidColorBrush(Color.FromRgb(0x26, 0xA6, 0x9A))
            : new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
    }


    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}