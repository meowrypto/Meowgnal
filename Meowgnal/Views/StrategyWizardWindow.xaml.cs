using System;
using System.Linq;
using System.Windows;
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