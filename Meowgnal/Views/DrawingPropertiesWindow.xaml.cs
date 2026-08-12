using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Meowgnal.Models;
using Drawing = Meowgnal.Models.Drawing;

namespace Meowgnal.Views;

public partial class DrawingPropertiesWindow : Window
{
    private readonly Drawing _drawing;

    public DrawingPropertiesWindow(Drawing drawing)
    {
        InitializeComponent();
        _drawing = drawing;

        LabelBox.Text = drawing.Label;
        HexBox.Text = drawing.Color;
        AlertCheck.IsChecked = drawing.AlertOnCross;
        LockedCheck.IsChecked = drawing.IsLocked;
        HiddenCheck.IsChecked = !drawing.IsVisible;
        UpdateSwatch();
    }

    private void UpdateSwatch()
    {
        try { PreviewRect.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(HexBox.Text)); }
        catch { PreviewRect.Fill = new SolidColorBrush(Colors.Gray); }
    }

    private void Swatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string hex) return;
        HexBox.Text = hex;
        UpdateSwatch();
    }

    private void HexBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateSwatch();

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _drawing.Label = LabelBox.Text.Trim();
        _drawing.Color = HexBox.Text.Trim();
        _drawing.AlertOnCross = AlertCheck.IsChecked == true;
        _drawing.IsLocked = LockedCheck.IsChecked == true;
        _drawing.IsVisible = HiddenCheck.IsChecked != true;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}