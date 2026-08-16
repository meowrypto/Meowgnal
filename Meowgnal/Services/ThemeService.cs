using System;
using System.Windows;
using System.Windows.Media;
using Meowgnal.Models;

namespace Meowgnal.Services;

/// <summary>Manages theme switching (Dark / Light / System / Custom).</summary>
public static class ThemeService
{
    public static void ApplyTheme(AppSettings settings)
    {
        var theme = (settings.Theme ?? "dark").ToLowerInvariant();
        if (theme == "system")
            theme = IsWindowsDarkMode() ? "dark" : "light";

        Color background, panel, border, textPrimary, textSecondary, textMuted, accent;
        var up = Color.FromRgb(0x08, 0x99, 0x81);
        var down = Color.FromRgb(0xF2, 0x36, 0x45);

        switch (theme)
        {
            case "light":
                background = Color.FromRgb(0xFF, 0xFF, 0xFF);
                panel = Color.FromRgb(0xF8, 0xF9, 0xFD);
                border = Color.FromRgb(0xE0, 0xE3, 0xEB);
                textPrimary = Color.FromRgb(0x13, 0x17, 0x22);
                textSecondary = Color.FromRgb(0x43, 0x46, 0x51);
                textMuted = Color.FromRgb(0x78, 0x7B, 0x86);
                accent = Color.FromRgb(0x29, 0x62, 0xFF);
                break;

            case "custom":
                background = ParseColor(settings.CustomBackground, "#131722");
                panel = ParseColor(settings.CustomPanel, "#1E222D");
                border = ParseColor(settings.CustomBorder, "#2A2E39");
                textPrimary = ParseColor(settings.CustomTextPrimary, "#D1D4DC");
                textSecondary = textPrimary;
                textMuted = textPrimary;
                accent = ParseColor(settings.CustomAccent, "#2962FF");
                break;

            default:
                background = Color.FromRgb(0x13, 0x17, 0x22);
                panel = Color.FromRgb(0x1E, 0x22, 0x2D);
                border = Color.FromRgb(0x2A, 0x2E, 0x39);
                textPrimary = Color.FromRgb(0xD1, 0xD4, 0xDC);
                textSecondary = Color.FromRgb(0xB2, 0xB5, 0xBE);
                textMuted = Color.FromRgb(0x78, 0x7B, 0x86);
                accent = Color.FromRgb(0x29, 0x62, 0xFF);
                break;
        }

        var resources = Application.Current.Resources;
        resources["ColorBackground"] = background;
        resources["ColorPanel"] = panel;
        resources["ColorBorder"] = border;
        resources["ColorTextPrimary"] = textPrimary;
        resources["ColorTextSecondary"] = textSecondary;
        resources["ColorTextMuted"] = textMuted;
        resources["ColorAccent"] = accent;
        resources["ColorUp"] = up;
        resources["ColorDown"] = down;
    }

    /// <summary>Colors sent to chart.html; per-user chart overrides win over the theme.</summary>
    public static ChartThemeColors GetChartColors(AppSettings settings)
    {
        var theme = (settings.Theme ?? "dark").ToLowerInvariant();
        if (theme == "system")
            theme = IsWindowsDarkMode() ? "dark" : "light";

        var c = theme == "light" ? LightColors() : theme == "custom" ? CustomColors(settings) : DarkColors();

        if (!string.IsNullOrWhiteSpace(settings.ChartUpColor)) c.up = settings.ChartUpColor;
        if (!string.IsNullOrWhiteSpace(settings.ChartDownColor)) c.down = settings.ChartDownColor;
        if (!string.IsNullOrWhiteSpace(settings.ChartBackgroundColor)) c.background = settings.ChartBackgroundColor;
        if (!string.IsNullOrWhiteSpace(settings.ChartGridColor)) c.grid = settings.ChartGridColor;
        if (!string.IsNullOrWhiteSpace(settings.ChartBorderColor)) c.border = settings.ChartBorderColor;
        if (!string.IsNullOrWhiteSpace(settings.ChartCrosshairColor)) c.crosshair = settings.ChartCrosshairColor;
        return c;
    }

    private static ChartThemeColors DarkColors() => new()
    {
        background = "#131722",
        grid = "#1E222D",
        border = "#2A2E39",
        textPrimary = "#D1D4DC",
        textMuted = "#787B86",
        crosshair = "#758696",
        accent = "#2962FF",
        up = "#089981",
        down = "#F23645",
        volumeUp = "rgba(8, 153, 129, 0.35)",
        volumeDown = "rgba(242, 54, 69, 0.35)"
    };

    private static ChartThemeColors LightColors() => new()
    {
        background = "#FFFFFF",
        grid = "#F0F3FA",
        border = "#E0E3EB",
        textPrimary = "#131722",
        textMuted = "#787B86",
        crosshair = "#758696",
        accent = "#2962FF",
        up = "#089981",
        down = "#F23645",
        volumeUp = "rgba(8, 153, 129, 0.35)",
        volumeDown = "rgba(242, 54, 69, 0.35)"
    };

    private static ChartThemeColors CustomColors(AppSettings s) => new()
    {
        background = s.CustomBackground,
        grid = s.CustomPanel,
        border = s.CustomBorder,
        textPrimary = s.CustomTextPrimary,
        textMuted = s.CustomTextPrimary,
        accent = s.CustomAccent,
        up = "#089981",
        down = "#F23645",
        volumeUp = "rgba(8, 153, 129, 0.35)",
        volumeDown = "rgba(242, 54, 69, 0.35)"
    };

    private static Color ParseColor(string hex, string fallback)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hex) || hex.TrimStart('#').Length != 6)
                hex = fallback;

            hex = hex.TrimStart('#');
            var r = Convert.ToByte(hex.Substring(0, 2), 16);
            var g = Convert.ToByte(hex.Substring(2, 2), 16);
            var b = Convert.ToByte(hex.Substring(4, 2), 16);
            return Color.FromRgb(r, g, b);
        }
        catch
        {
            return (Color)ColorConverter.ConvertFromString(fallback);
        }
    }

    private static bool IsWindowsDarkMode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int v)
                return v == 0;
        }
        catch
        {
        }
        return false;
    }
}

/// <summary>Plain color payload serialized to chart.html.</summary>
public sealed class ChartThemeColors
{
    public string background { get; set; } = "#131722";
    public string grid { get; set; } = "#1E222D";
    public string border { get; set; } = "#2A2E39";
    public string textPrimary { get; set; } = "#D1D4DC";
    public string textMuted { get; set; } = "#787B86";
    public string crosshair { get; set; } = "#758696";
    public string accent { get; set; } = "#2962FF";
    public string up { get; set; } = "#089981";
    public string down { get; set; } = "#F23645";
    public string volumeUp { get; set; } = "rgba(8, 153, 129, 0.35)";
    public string volumeDown { get; set; } = "rgba(242, 54, 69, 0.35)";
}