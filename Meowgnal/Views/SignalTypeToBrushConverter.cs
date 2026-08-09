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
            ? new SolidColorBrush(Color.FromRgb(0x08, 0x99, 0x81))
            : new SolidColorBrush(Color.FromRgb(0xF2, 0x36, 0x45));
    }

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}