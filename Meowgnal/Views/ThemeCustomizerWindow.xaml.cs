using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Meowgnal.Models;
using Meowgnal.Services;

namespace Meowgnal.Views;

public partial class ThemeCustomizerWindow : Window
{
    private static readonly string[] Palette =
    {
        "#131722", "#0F1420", "#101322", "#1E222D", "#2A2E39", "#363A45",
        "#FFFFFF", "#F8F9FD", "#E0E3EB", "#D1D4DC", "#B2B5BE", "#787B86",
        "#2962FF", "#089981", "#F23645", "#FF9800", "#9C27B0", "#00BCD4",
        "#4CAF50", "#795548"
    };

    private readonly AppSettings _settings = SettingsStorageService.Load();
    private string _bg;
    private string _panel;
    private string _border;
    private string _text;
    private string _accent;

    public ThemeCustomizerWindow()
    {
        InitializeComponent();

        _bg = _settings.CustomBackground;
        _panel = _settings.CustomPanel;
        _border = _settings.CustomBorder;
        _text = _settings.CustomTextPrimary;
        _accent = _settings.CustomAccent;

        BuildPalette(PaletteBackground, "bg");
        BuildPalette(PalettePanel, "panel");
        BuildPalette(PaletteBorder, "border");
        BuildPalette(PaletteText, "text");
        BuildPalette(PaletteAccent, "accent");

        RefreshAll();
    }

    private void BuildPalette(WrapPanel panel, string key)
    {
        foreach (var hex in Palette)
        {
            var btn = new Button
            {
                Style = (Style)Resources["SwatchButton"],
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                Tag = key + "|" + hex,
                ToolTip = hex
            };
            btn.Click += Palette_Click;
            panel.Children.Add(btn);
        }
    }

    private void Palette_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;
        var parts = tag.Split('|');
        if (parts.Length == 2) SetColor(parts[0], parts[1]);
    }

    private void Hex_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box || box.Tag is not string key) return;
        var hex = box.Text?.Trim() ?? "";
        if (IsValidHex(hex)) SetColor(key, hex);
    }

    private static bool IsValidHex(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.TrimStart('#');
        if (s.Length != 6) return false;
        foreach (var c in s)
            if (!Uri.IsHexDigit(c)) return false;
        return true;
    }

    private void SetColor(string key, string hex)
    {
        switch (key)
        {
            case "bg": _bg = hex; SwatchBackground.Fill = Brush(hex); Sync(HexBackground, hex); break;
            case "panel": _panel = hex; SwatchPanel.Fill = Brush(hex); Sync(HexPanel, hex); break;
            case "border": _border = hex; SwatchBorder.Fill = Brush(hex); Sync(HexBorder, hex); break;
            case "text": _text = hex; SwatchText.Fill = Brush(hex); Sync(HexText, hex); break;
            case "accent": _accent = hex; SwatchAccent.Fill = Brush(hex); Sync(HexAccent, hex); break;
        }
    }

    private static void Sync(TextBox box, string hex)
    {
        if (!box.IsFocused && box.Text != hex) box.Text = hex;
    }

    private static Brush Brush(string hex) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

    private void RefreshAll()
    {
        SetColor("bg", _bg);
        SetColor("panel", _panel);
        SetColor("border", _border);
        SetColor("text", _text);
        SetColor("accent", _accent);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _bg = "#131722";
        _panel = "#1E222D";
        _border = "#2A2E39";
        _text = "#D1D4DC";
        _accent = "#2962FF";
        RefreshAll();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.CustomBackground = _bg;
        _settings.CustomPanel = _panel;
        _settings.CustomBorder = _border;
        _settings.CustomTextPrimary = _text;
        _settings.CustomAccent = _accent;
        _settings.Theme = "custom";
        SettingsStorageService.Save(_settings);

        DialogResult = true;
        Close();
    }
}