using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Meowgnal.Models;
using Meowgnal.Services;

namespace Meowgnal.Views;

public partial class ChartSettingsWindow : Window
{
    private static readonly string[] Palette =
    {
        "#089981", "#F23645", "#2962FF", "#131722", "#1E222D", "#2A2E39",
        "#363A45", "#787B86", "#FFFFFF", "#F0F3FA", "#E0E3EB", "#758696"
    };

    private readonly AppSettings _settings = SettingsStorageService.Load();
    private PriceAlertsFile _alerts = PriceAlertStorageService.Load();

    private readonly Dictionary<string, string> _values = new();
    private readonly Dictionary<string, (Rectangle Swatch, TextBox Hex)> _rows = new();
    private readonly StackPanel _alertsPanel = new();

    public ChartSettingsWindow()
    {
        InitializeComponent();

        _values["up"] = _settings.ChartUpColor;
        _values["down"] = _settings.ChartDownColor;
        _values["background"] = _settings.ChartBackgroundColor;
        _values["grid"] = _settings.ChartGridColor;
        _values["border"] = _settings.ChartBorderColor;
        _values["crosshair"] = _settings.ChartCrosshairColor;

        AddColorRow("up", "Up candles");
        AddColorRow("down", "Down candles");
        AddColorRow("background", "Chart background");
        AddColorRow("grid", "Grid lines");
        AddColorRow("border", "Scale borders");
        AddColorRow("crosshair", "Crosshair");

        RootPanel.Children.Add(new TextBlock
        {
            Text = "🔔 Price alerts",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimary"),
            Margin = new Thickness(0, 16, 0, 8)
        });
        RootPanel.Children.Add(_alertsPanel);
        RefreshAlertsList();
    }

    #region Custom title bar

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { ToggleMaximize(); return; }
        if (WindowState == WindowState.Maximized)
        {
            var point = PointToScreen(e.GetPosition(this));
            WindowState = WindowState.Normal;
            Left = point.X - Width / 2;
            Top = point.Y - 15;
        }
        DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaximizeButton.Content = "⛶";
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeButton.Content = "❐";
        }
    }

    #endregion

    private void AddColorRow(string key, string label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };

        var swatch = new Rectangle
        {
            Width = 34,
            Height = 24,
            RadiusX = 4,
            RadiusY = 4,
            Stroke = (Brush)FindResource("BorderColor"),
            StrokeThickness = 1
        };
        row.Children.Add(swatch);

        row.Children.Add(new TextBlock
        {
            Text = label,
            Width = 120,
            Foreground = (Brush)FindResource("TextPrimary"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        });

        var hex = new TextBox
        {
            Style = (Style)FindResource("HexBox"),
            Width = 90,
            Tag = key
        };
        hex.TextChanged += Hex_TextChanged;
        row.Children.Add(hex);

        _rows[key] = (swatch, hex);
        RootPanel.Children.Add(row);

        var palette = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        foreach (var p in Palette)
        {
            var btn = new Button
            {
                Style = (Style)FindResource("SwatchButton"),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(p)),
                Tag = key + "|" + p,
                ToolTip = p
            };
            btn.Click += Palette_Click;
            palette.Children.Add(btn);
        }
        RootPanel.Children.Add(palette);

        RefreshRow(key);
    }

    private void Palette_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;
        var parts = tag.Split('|');
        if (parts.Length == 2)
        {
            _values[parts[0]] = parts[1];
            RefreshRow(parts[0]);
        }
    }

    private void Hex_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box || box.Tag is not string key) return;
        var text = (box.Text ?? "").Trim();

        if (text == "")
        {
            if (_values[key] != "")
            {
                _values[key] = "";
                RefreshRow(key);
            }
            return;
        }

        if (IsValidHex(text))
        {
            var normalized = "#" + text.TrimStart('#').ToUpperInvariant();
            if (_values[key] != normalized)
            {
                _values[key] = normalized;
                RefreshRow(key);
            }
        }
    }

    private void RefreshRow(string key)
    {
        if (!_rows.TryGetValue(key, out var row)) return;
        var value = _values[key] ?? "";
        if (row.Hex.Text != value) row.Hex.Text = value;

        try
        {
            row.Swatch.Fill = string.IsNullOrWhiteSpace(value)
                ? new SolidColorBrush(Colors.Transparent)
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        }
        catch
        {
            row.Swatch.Fill = new SolidColorBrush(Colors.Transparent);
        }
    }

    private static bool IsValidHex(string s)
    {
        s = s.TrimStart('#');
        if (s.Length != 6) return false;
        foreach (var c in s)
            if (!Uri.IsHexDigit(c)) return false;
        return true;
    }

    private void RefreshAlertsList()
    {
        _alertsPanel.Children.Clear();

        if (_alerts.Alerts.Count == 0)
        {
            _alertsPanel.Children.Add(new TextBlock
            {
                Text = "No alerts yet — right-click the chart to add one.",
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 11
            });
            return;
        }

        foreach (var alert in _alerts.Alerts)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            row.Children.Add(new TextBlock
            {
                Text = $"{alert.Symbol}  @  {alert.Price:N2}",
                Foreground = (Brush)FindResource("TextPrimary"),
                VerticalAlignment = VerticalAlignment.Center,
                Width = 260
            });
            var del = new Button
            {
                Content = "✕",
                Style = (Style)FindResource("SecondaryButton"),
                Padding = new Thickness(8, 2, 8, 2),
                Tag = alert
            };
            del.Click += DeleteAlert_Click;
            row.Children.Add(del);
            _alertsPanel.Children.Add(row);
        }
    }

    private void DeleteAlert_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not PriceAlert alert) return;
        _alerts.Alerts.Remove(alert);
        PriceAlertStorageService.Save(_alerts);
        RefreshAlertsList();
    }

    private void ResetColors_Click(object sender, RoutedEventArgs e)
    {
        foreach (var key in new List<string>(_values.Keys))
        {
            _values[key] = "";
            RefreshRow(key);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.ChartUpColor = _values["up"];
        _settings.ChartDownColor = _values["down"];
        _settings.ChartBackgroundColor = _values["background"];
        _settings.ChartGridColor = _values["grid"];
        _settings.ChartBorderColor = _values["border"];
        _settings.ChartCrosshairColor = _values["crosshair"];
        SettingsStorageService.Save(_settings);

        DialogResult = true;
        Close();
    }
}