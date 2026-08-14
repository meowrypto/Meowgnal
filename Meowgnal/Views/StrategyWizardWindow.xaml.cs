using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Meowgnal.Models;
using Meowgnal.Services;

namespace Meowgnal.Views;

public partial class StrategyWizardWindow : Window
{
    private readonly string _symbol;

    public StrategyWizardWindow(string symbol)
    {
        InitializeComponent();
        _symbol = string.IsNullOrWhiteSpace(symbol) ? "BTC/USDT" : symbol;
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

    private string SelectedStyle =>
        StyleReversal.IsChecked == true ? "Reversal" :
        StyleBreakout.IsChecked == true ? "Breakout" : "Trend";

    private string SelectedSpeed =>
        SpeedScalp.IsChecked == true ? "Scalp" :
        SpeedLongTerm.IsChecked == true ? "Long-term" : "Swing";

    private string SelectedCaution =>
        CautionCareful.IsChecked == true ? "Careful" :
        CautionAggressive.IsChecked == true ? "Aggressive" : "Balanced";

    private void SaveAndUse_Click(object sender, RoutedEventArgs e)
    {
        var strategy = StrategyWizardService.Build(SelectedStyle, SelectedSpeed, SelectedCaution, _symbol);

        // Unique name: append (2), (3), ... if needed
        var existing = StrategyStorageService.LoadAll();
        var baseName = strategy.Name;
        var counter = 1;
        while (existing.Any(s => string.Equals(s.Name, strategy.Name, StringComparison.OrdinalIgnoreCase)))
        {
            counter++;
            strategy.Name = $"{baseName} ({counter})";
        }

        StrategyStorageService.Save(strategy);
        NotificationService.ShowToast("Meowgnal", $"Added '{strategy.Name}' for {_symbol}.");
        DialogResult = true;
        Close();
    }

    private void OpenInBuilder_Click(object sender, RoutedEventArgs e)
    {
        var strategy = StrategyWizardService.Build(SelectedStyle, SelectedSpeed, SelectedCaution, _symbol);
        var builder = new StrategyBuilderWindow(strategy) { Owner = this };
        builder.ShowDialog();
    }
}