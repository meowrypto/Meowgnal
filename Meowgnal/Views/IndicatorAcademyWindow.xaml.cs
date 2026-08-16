using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Meowgnal.Models;
using Meowgnal.Services;

namespace Meowgnal.Views;

public partial class IndicatorAcademyWindow : Window
{
    private IndicatorInfo? _selected;

    // Same category order as IndicatorPanel.
    private static readonly string[] CategoryOrder =
    {
        "Moving Averages", "Oscillators", "Volatility", "Volume", "Trend", "Fundamental"
    };

    public IndicatorAcademyWindow(string? preselectType = null)
    {
        InitializeComponent();
        BuildCategories();

        if (!string.IsNullOrEmpty(preselectType))
        {
            var info = IndicatorRegistry.All.FirstOrDefault(i =>
                string.Equals(i.Type, preselectType, StringComparison.OrdinalIgnoreCase));
            if (info is not null) SelectIndicator(info);
        }
    }

    // Safe resource lookup: never crashes if the key is missing.
    private Brush FindBrush(string key)
    {
        try
        {
            var res = FindResource(key);
            if (res is SolidColorBrush brush) return brush;
        }
        catch { }
        return Brushes.Gray;
    }

    // Safe style lookup: falls back to default Button style if missing.
    private Style FindStyle(string key)
    {
        try
        {
            var res = FindResource(key);
            if (res is Style style) return style;
        }
        catch { }
        return null!;
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

    private void BuildCategories(string filter = "")
    {
        CategoriesPanel.Children.Clear();
        var q = filter.Trim().ToLowerInvariant();

        foreach (var cat in CategoryOrder)
        {
            var items = IndicatorRegistry.All
                .Where(i => i.SubCategory == cat)
                .Where(i => string.IsNullOrEmpty(q) ||
                            i.Type.ToLowerInvariant().Contains(q) ||
                            i.Label.ToLowerInvariant().Contains(q) ||
                            i.Description.ToLowerInvariant().Contains(q))
                .ToList();

            if (items.Count == 0) continue;

            var header = new TextBlock
            {
                Text = cat.ToUpperInvariant(),
                Foreground = FindBrush("TextMuted"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(4, 14, 0, 6)
            };
            CategoriesPanel.Children.Add(header);

            foreach (var info in items)
            {
                var hasContent = IndicatorEducationRepository.Get(info.Type) is not null;

                // Use a plain button with a simple local look (no dependency on MainWindow styles).
                var btn = new Button
                {
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(8, 6, 8, 6),
                    Margin = new Thickness(0, 2, 0, 0),
                    Tag = info,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Cursor = Cursors.Hand
                };
                btn.Click += Item_Click;

                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock
                {
                    Text = info.Label,
                    Foreground = FindBrush("TextPrimary"),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                });
                sp.Children.Add(new TextBlock
                {
                    Text = hasContent ? "" : "  📝",
                    FontSize = 11,
                    Foreground = FindBrush("TextMuted"),
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = hasContent ? "" : "Educational guide coming soon"
                });
                btn.Content = sp;

                // Highlight the currently selected row
                if (_selected is not null &&
                    string.Equals(_selected.Type, info.Type, StringComparison.OrdinalIgnoreCase))
                {
                    btn.Foreground = FindBrush("Accent");
                    btn.FontWeight = FontWeights.SemiBold;
                }

                CategoriesPanel.Children.Add(btn);
            }
        }

        if (CategoriesPanel.Children.Count == 0)
        {
            CategoriesPanel.Children.Add(new TextBlock
            {
                Text = "No indicators match your search.",
                Foreground = FindBrush("TextMuted"),
                FontSize = 12,
                Margin = new Thickness(4, 20, 0, 0)
            });
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        BuildCategories(SearchBox.Text ?? "");
    }

    private void Item_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not IndicatorInfo info) return;
        SelectIndicator(info);
        BuildCategories(SearchBox.Text ?? "");
    }

    private void SelectIndicator(IndicatorInfo info)
    {
        _selected = info;
        var ed = IndicatorEducationRepository.Get(info.Type);

        if (ed is null)
        {
            EmptyState.Visibility = Visibility.Collapsed;
            ArticleState.Visibility = Visibility.Collapsed;
            ComingSoonState.Visibility = Visibility.Visible;
            ComingSoonTitle.Text = info.Label;
            HintText.Text = $"No guide for '{info.Label}' yet.";
            TryItButton.IsEnabled = true;
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        ComingSoonState.Visibility = Visibility.Collapsed;
        ArticleState.Visibility = Visibility.Visible;

        ArticleTitle.Text = info.Label;
        ArticleSubtitle.Text = info.SubCategory + "  •  " +
                               (info.HasNoPeriod ? "No period parameter" : $"Default period: {info.DefaultPeriod}");

        WhatIsItText.Text = ed.WhatIsIt;
        WhenToUseText.Text = ed.WhenToUse;
        ParamsText.Text = ed.RecommendedDefaultParams;

        PairedPanel.Children.Clear();
        foreach (var p in ed.BestPairedWith)
            PairedPanel.Children.Add(MakeBullet("🤝", p));

        TrapsPanel.Children.Clear();
        foreach (var t in ed.CommonTraps)
            TrapsPanel.Children.Add(MakeBullet("⚠️", t));

        HintText.Text = $"Reading about '{info.Label}'. Click below to try it in the Strategy Builder.";
        TryItButton.IsEnabled = true;
    }

    private UIElement MakeBullet(string icon, string text)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        sp.Children.Add(new TextBlock
        {
            Text = icon,
            FontSize = 13,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Top
        });
        sp.Children.Add(new TextBlock
        {
            Text = "•  " + text,
            Foreground = FindBrush("TextPrimary"),
            FontSize = 13,
            LineHeight = 19,
            TextWrapping = TextWrapping.Wrap,
            Width = 620
        });
        return sp;
    }

    private void TryIt_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;

        var builder = new StrategyBuilderWindow { Owner = this };
        builder.AddIndicator(_selected);
        builder.ShowDialog();
    }
}