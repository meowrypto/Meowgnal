using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Meowgnal.Models;
using Drawing = Meowgnal.Models.Drawing;

namespace Meowgnal.Views;

public partial class DrawingPropertiesWindow : Window
{
    private readonly Drawing _drawing;
    private readonly List<TextBox> _priceBoxes = new();

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

        // Thickness options (1-4 pixels, TradingView standard)
        for (var w = 1; w <= 4; w++) WidthCombo.Items.Add(w);
        WidthCombo.SelectedItem = drawing.LineWidth is >= 1 and <= 4 ? drawing.LineWidth : 2;

        // Line style options (TradingView standard)
        StyleCombo.Items.Add("solid");
        StyleCombo.Items.Add("dashed");
        StyleCombo.Items.Add("dotted");
        StyleCombo.SelectedItem = drawing.LineStyle;

        // Build coordinate rows: time is read-only, price is editable
        for (var i = 0; i < drawing.Points.Count; i++)
        {
            var p = drawing.Points[i];
            var row = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10, GridUnitType.Star) });

            var header = new TextBlock
            {
                Text = $"P{i + 1}",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("TextPrimary")
            };
            Grid.SetColumn(header, 0);
            row.Children.Add(header);

            var time = new TextBlock
            {
                Text = System.DateTimeOffset.FromUnixTimeSeconds(p.TimeUnix).UtcDateTime.ToString("yyyy/MM/dd HH:mm"),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 11
            };
            Grid.SetColumn(time, 1);
            row.Children.Add(time);

            var priceBox = new TextBox
            {
                Text = p.Price.ToString(CultureInfo.InvariantCulture),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 120
            };
            Grid.SetColumn(priceBox, 2);
            row.Children.Add(priceBox);
            _priceBoxes.Add(priceBox);

            PointsPanel.Children.Add(row);
        }
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

        // Save thickness and line style
        _drawing.LineWidth = WidthCombo.SelectedItem is int w ? w : 2;
        _drawing.LineStyle = StyleCombo.SelectedItem as string ?? "solid";

        // Save edited prices
        for (var i = 0; i < _drawing.Points.Count && i < _priceBoxes.Count; i++)
        {
            if (decimal.TryParse(_priceBoxes[i].Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) && price > 0)
                _drawing.Points[i].Price = price;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}