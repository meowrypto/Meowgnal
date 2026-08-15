using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
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

    private string _bg;
    private string _panel;
    private string _border;
    private string _text;
    private string _accent;

    // True while we programmatically fill a hex box (prevents live re-apply).
    private bool _syncing;

    public ThemeCustomizerWindow()
    {
        InitializeComponent();

        var s = SettingsStorageService.Load();
        _bg = s.CustomBackground;
        _panel = s.CustomPanel;
        _border = s.CustomBorder;
        _text = s.CustomTextPrimary;
        _accent = s.CustomAccent;

        BuildPalette(PaletteBackground, "bg");
        BuildPalette(PalettePanel, "panel");
        BuildPalette(PaletteBorder, "border");
        BuildPalette(PaletteText, "text");
        BuildPalette(PaletteAccent, "accent");

        // Paint ONLY this window's preview swatches — never the live app.
        RefreshAll();

        // If the user closes without saving, revert any live preview changes.
        Closing += (_, _) =>
        {
            if (DialogResult != true)
                ThemeService.ApplyTheme(SettingsStorageService.Load());
        };
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
        if (parts.Length == 2) SetColor(parts[0], parts[1], true);
    }

    private void Hex_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing) return;
        if (sender is not TextBox box || box.Tag is not string key) return;
        var hex = box.Text?.Trim() ?? "";
        if (IsValidHex(hex)) SetColor(key, hex, true);
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

    // live=false: only paint this window's swatches (safe on open).
    // live=true:  also repaint the whole app (only after a real user click).
    private void SetColor(string key, string hex, bool live)
    {
        switch (key)
        {
            case "bg": _bg = hex; SwatchBackground.Fill = Brush(hex); Sync(HexBackground, hex); if (live) ApplyLive("ColorBackground", hex); break;
            case "panel": _panel = hex; SwatchPanel.Fill = Brush(hex); Sync(HexPanel, hex); if (live) ApplyLive("ColorPanel", hex); break;
            case "border": _border = hex; SwatchBorder.Fill = Brush(hex); Sync(HexBorder, hex); if (live) ApplyLive("ColorBorder", hex); break;
            case "text": _text = hex; SwatchText.Fill = Brush(hex); Sync(HexText, hex); if (live) ApplyLive("ColorTextPrimary", hex); break;
            case "accent": _accent = hex; SwatchAccent.Fill = Brush(hex); Sync(HexAccent, hex); if (live) ApplyLive("ColorAccent", hex); break;
        }
    }

    private static void ApplyLive(string resourceKey, string hex)
    {
        try { Application.Current.Resources[resourceKey] = (Color)ColorConverter.ConvertFromString(hex); }
        catch { }
    }

    private void Sync(TextBox box, string hex)
    {
        if (!box.IsFocused && box.Text != hex)
        {
            _syncing = true;
            box.Text = hex;
            _syncing = false;
        }
    }

    private static Brush Brush(string hex) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

    private void RefreshAll()
    {
        SetColor("bg", _bg, false);
        SetColor("panel", _panel, false);
        SetColor("border", _border, false);
        SetColor("text", _text, false);
        SetColor("accent", _accent, false);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _bg = "#131722";
        _panel = "#1E222D";
        _border = "#2A2E39";
        _text = "#D1D4DC";
        _accent = "#2962FF";

        SetColor("bg", _bg, true);
        SetColor("panel", _panel, true);
        SetColor("border", _border, true);
        SetColor("text", _text, true);
        SetColor("accent", _accent, true);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var s = SettingsStorageService.Load();
        s.CustomBackground = _bg;
        s.CustomPanel = _panel;
        s.CustomBorder = _border;
        s.CustomTextPrimary = _text;
        s.CustomAccent = _accent;
        s.Theme = "custom";
        SettingsStorageService.Save(s);

        DialogResult = true;
        Close();
    }
}