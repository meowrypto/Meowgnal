using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Meowgnal.Views;

// Hides the 🔍 autopsy icon when the trade explanation is empty.
public sealed class ExplanationVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}