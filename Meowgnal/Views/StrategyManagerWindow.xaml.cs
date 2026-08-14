using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Meowgnal.Models;
using Meowgnal.Services;

namespace Meowgnal.Views;

public partial class StrategyManagerWindow : Window
{
    private readonly string _symbol;

    public StrategyManagerWindow(string symbol)
    {
        InitializeComponent();
        _symbol = string.IsNullOrWhiteSpace(symbol) ? "BTC/USDT" : symbol;
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        ListPanel.Children.Clear();

        var strategies = StrategyStorageService.LoadAll()
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (strategies.Count == 0)
        {
            ListPanel.Children.Add(new TextBlock
            {
                Text = "No saved strategies yet. Use \"+ New strategy\" or \"Templates\" to create one.",
                Foreground = (Brush)FindResource("TextMuted"),
                Margin = new Thickness(6),
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var s in strategies)
        {
            var entryCount = s.EntryRules?.Conditions?.Count ?? 0;
            var exitCount = s.ExitRules?.Conditions?.Count ?? 0;

            var card = new Border
            {
                Background = (Brush)FindResource("BorderColor"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel();
            left.Children.Add(new TextBlock
            {
                Text = s.Name,
                Foreground = (Brush)FindResource("TextPrimary"),
                FontSize = 14,
                FontWeight = FontWeights.Bold
            });
            left.Children.Add(new TextBlock
            {
                Text = $"{s.Symbol} · {s.Timeframe} · {s.DataSource}",
                Foreground = (Brush)FindResource("TextSecondary"),
                FontSize = 11,
                Margin = new Thickness(0, 3, 0, 0)
            });
            left.Children.Add(new TextBlock
            {
                Text = $"{entryCount} entry / {exitCount} exit conditions",
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var editBtn = new Button { Content = "✏️ Edit", Style = (Style)FindResource("SecondaryButton"), Padding = new Thickness(10, 5, 10, 5), Tag = s, Margin = new Thickness(0, 0, 6, 0) };
            editBtn.Click += Edit_Click;
            right.Children.Add(editBtn);

            var copyBtn = new Button { Content = "📋 Copy", Style = (Style)FindResource("TertiaryButton"), Padding = new Thickness(10, 5, 10, 5), Tag = s, Margin = new Thickness(0, 0, 6, 0) };
            copyBtn.Click += Copy_Click;
            right.Children.Add(copyBtn);

            var exportBtn = new Button { Content = "📤 Export", Style = (Style)FindResource("TertiaryButton"), Padding = new Thickness(10, 5, 10, 5), Tag = s, Margin = new Thickness(0, 0, 6, 0) };
            exportBtn.Click += Export_Click;
            right.Children.Add(exportBtn);

            var deleteBtn = new Button { Content = "🗑 Delete", Style = (Style)FindResource("TertiaryButton"), Padding = new Thickness(10, 5, 10, 5), Tag = s, Foreground = (Brush)FindResource("Down") };
            deleteBtn.Click += Delete_Click;

            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            card.Child = grid;
            ListPanel.Children.Add(card);
        }
    }

    private void NewStrategy_Click(object sender, RoutedEventArgs e)
    {
        new StrategyBuilderWindow { Owner = this }.ShowDialog();
        Refresh();
    }

    private void Templates_Click(object sender, RoutedEventArgs e)
    {
        var store = new TemplateStoreWindow(_symbol) { Owner = this };
        store.ShowDialog();
        Refresh();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not StrategyDefinition s) return;
        var builder = new StrategyBuilderWindow(s, StrategyBuilderWindow.BuilderOpenMode.Edit) { Owner = this };
        builder.ShowDialog();
        Refresh();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not StrategyDefinition s) return;
        var builder = new StrategyBuilderWindow(s, StrategyBuilderWindow.BuilderOpenMode.Copy) { Owner = this };
        builder.ShowDialog();
        Refresh();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not StrategyDefinition s) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export strategy",
            Filter = "Strategy file (*.mgstrat)|*.mgstrat|JSON file (*.json)|*.json",
            FileName = SanitizeFileName(s.Name) + ".mgstrat"
        };
        if (dialog.ShowDialog() != true) return;

        StrategyStorageService.Export(s, dialog.FileName);
        NotificationService.ShowToast("Meowgnal", $"Exported '{s.Name}'.");
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import strategy",
            Filter = "Strategy files (*.mgstrat;*.json)|*.mgstrat;*.json|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        var imported = StrategyStorageService.Import(dialog.FileName);
        if (imported is null)
        {
            MessageBox.Show("Could not read a strategy from this file.", "Meowgnal",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        NotificationService.ShowToast("Meowgnal", $"Imported '{imported.Name}'.");
        Refresh();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not StrategyDefinition s) return;

        var res = MessageBox.Show($"Are you sure you want to delete '{s.Name}'?", "Meowgnal",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        StrategyStorageService.Delete(s.StrategyId);
        NotificationService.ShowToast("Meowgnal", $"Deleted '{s.Name}'.");
        Refresh();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}