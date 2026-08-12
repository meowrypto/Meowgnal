using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Meowgnal.Models;
using Meowgnal.Services;

namespace Meowgnal.Views;

public partial class PortfolioWindow : Window
{
    private readonly AppSettings _settings;

    public PortfolioWindow()
    {
        InitializeComponent();
        _settings = SettingsStorageService.Load();

        MaxTotalBox.Text = _settings.PortfolioMaxTotalPositions.ToString();
        MaxPerStrategyBox.Text = _settings.PortfolioMaxPositionsPerStrategy.ToString();

        var strategies = StrategyStorageService.LoadAll();
        foreach (var s in strategies)
        {
            var cb = new CheckBox
            {
                Content = $"{s.Name}  —  {s.Symbol} ({s.Timeframe})",
                Tag = s.StrategyId,
                IsChecked = _settings.PortfolioEnabledStrategyIds.Contains(s.StrategyId)
            };
            StrategiesPanel.Children.Add(cb);
        }

        if (strategies.Count == 0)
        {
            StrategiesPanel.Children.Add(new TextBlock
            {
                Text = "No strategies found. Create at least one strategy first.",
                Foreground = (System.Windows.Media.Brush)FindResource("TextSecondary"),
                FontSize = 12
            });
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.PortfolioMaxTotalPositions = int.TryParse(MaxTotalBox.Text, out var t) ? t : 0;
        _settings.PortfolioMaxPositionsPerStrategy = int.TryParse(MaxPerStrategyBox.Text, out var p) ? p : 0;

        _settings.PortfolioEnabledStrategyIds = StrategiesPanel.Children
            .OfType<CheckBox>()
            .Where(cb => cb.IsChecked == true && cb.Tag is string id)
            .Select(cb => (string)cb.Tag!)
            .ToList();

        SettingsStorageService.Save(_settings);

        MessageBox.Show(
            "Portfolio settings saved. Auto-trade will now respect these limits.",
            "Saved",
            MessageBoxButton.OK, MessageBoxImage.Information);

        DialogResult = true;
        Close();
    }
}