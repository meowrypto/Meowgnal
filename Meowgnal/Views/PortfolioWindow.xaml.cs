using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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