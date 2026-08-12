using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Meowgnal.Views;

public class PnLColorConverter : IValueConverter
{
    // TradingView palette: green profit, red loss, neutral gray
    private static readonly SolidColorBrush ProfitBrush = new(Color.FromRgb(0x08, 0x99, 0x81));
    private static readonly SolidColorBrush LossBrush = new(Color.FromRgb(0xF2, 0x36, 0x45));
    private static readonly SolidColorBrush NeutralBrush = new(Color.FromRgb(0xD1, 0xD4, 0xDC));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            decimal d when d > 0 => ProfitBrush,
            decimal d when d < 0 => LossBrush,
            _ => NeutralBrush
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}