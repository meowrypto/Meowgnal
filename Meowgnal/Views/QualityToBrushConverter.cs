using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Meowgnal.Views;

public class QualityToBrushConverter : IValueConverter
{
    // TradingView palette: green for A+/A, yellow for B, red for C/D, gray for unknown
    private static readonly SolidColorBrush APlusBrush = new(Color.FromRgb(0x08, 0x99, 0x81));
    private static readonly SolidColorBrush ABrush = new(Color.FromRgb(0x26, 0xA6, 0x9A));
    private static readonly SolidColorBrush BBrush = new(Color.FromRgb(0xFF, 0xB7, 0x4D));
    private static readonly SolidColorBrush CBrush = new(Color.FromRgb(0xF2, 0x36, 0x45));
    private static readonly SolidColorBrush DBrush = new(Color.FromRgb(0xD3, 0x2D, 0x3A));
    private static readonly SolidColorBrush UnknownBrush = new(Color.FromRgb(0x78, 0x7B, 0x86));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            "A+" => APlusBrush,
            "A" => ABrush,
            "B" => BBrush,
            "C" => CBrush,
            "D" => DBrush,
            _ => UnknownBrush
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}