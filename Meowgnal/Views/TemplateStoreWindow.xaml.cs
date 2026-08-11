using Meowgnal.DataProviders;
using Meowgnal.Engine;
using Meowgnal.Models;
using Meowgnal.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Meowgnal.Views;

public partial class TemplateStoreWindow : Window
{
    private static readonly Dictionary<string, (double WinRate, int Trades, double ReturnPct)> _statsCache = new();

    private readonly string _symbol;
    private readonly ObservableCollection<TemplateCardViewModel> _cards = new();

    public TemplateStoreWindow(string symbol)
    {
        InitializeComponent();

        _symbol = string.IsNullOrWhiteSpace(symbol) ? "BTC/USDT" : symbol;
        ActiveSymbolText.Text = _symbol;
        TemplatesList.ItemsSource = _cards;

        Loaded += (_, _) => _ = LoadTemplatesAsync();
    }

    private async Task LoadTemplatesAsync()
    {
        var templates = StrategyTemplateCatalog.GetAll();

        foreach (var t in templates)
        {
            var card = new TemplateCardViewModel(t)
            {
                WinRate = "…",
                TradeCount = "…",
                ReturnPct = "…"
            };
            _cards.Add(card);
        }

        foreach (var card in _cards.ToList())
        {
            _ = RunBacktestForCardAsync(card);
        }
    }

    private async Task RunBacktestForCardAsync(TemplateCardViewModel card)
    {
        var cacheKey = $"{_symbol}|{card.Template.Name}";
        if (_statsCache.TryGetValue(cacheKey, out var cached))
        {
            ApplyStats(card, cached.WinRate, cached.Trades, cached.ReturnPct);
            return;
        }

        try
        {
            IDataProvider provider = new BinanceDataProvider();
            var bars = await provider.GetHistoricalCandlesAsync(_symbol, card.Template.DefaultTimeframe, limit: 500);

            if (bars.Count < 50)
            {
                ApplyStats(card, double.NaN, 0, double.NaN);
                return;
            }

            var strategy = card.Template.BuildStrategy!(_symbol);
            var result = BacktestEngine.Run(strategy, bars, startingBalance: 10000m, feePercent: 0.1m, slippagePercent: 0.05m);

            var winRate = result.WinRatePercent;
            var trades = result.Trades.Count;
            var returnPct = (double)((result.FinalBalance - 10000m) / 100m);

            _statsCache[cacheKey] = (winRate, trades, returnPct);
            ApplyStats(card, winRate, trades, returnPct);
        }
        catch
        {
            ApplyStats(card, double.NaN, 0, double.NaN);
        }
    }

    private void ApplyStats(TemplateCardViewModel card, double winRate, int trades, double returnPct)
    {
        if (double.IsNaN(winRate) || trades == 0)
        {
            card.WinRate = "—";
            card.TradeCount = "0";
            card.ReturnPct = "—";
            card.ReturnColor = (Brush)FindResource("TextMuted");
            return;
        }

        card.WinRate = $"{winRate:N0}%";
        card.TradeCount = trades.ToString();
        card.ReturnPct = $"{returnPct:+0.0;-0.0} %";
        card.ReturnColor = returnPct >= 0 ? (Brush)FindResource("Up") : (Brush)FindResource("Down");
    }

    private void UseTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not TemplateCardViewModel card) return;

        var strategy = card.Template.BuildStrategy!(_symbol);
        strategy.StrategyId = Guid.NewGuid().ToString("N");
        strategy.DataSource = "binance";

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

    private void CustomizeTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not TemplateCardViewModel card) return;

        var strategy = card.Template.BuildStrategy!(_symbol);
        strategy.Name = $"{strategy.Name} (Custom)";

        var builder = new StrategyBuilderWindow(strategy) { Owner = this };
        builder.ShowDialog();
    }

    private void OpenWizard_Click(object sender, RoutedEventArgs e)
    {
        var wizard = new StrategyWizardWindow(_symbol) { Owner = this };
        if (wizard.ShowDialog() == true)
        {
            DialogResult = true;
            Close();
        }
    }

    private void BuildFromScratch_Click(object sender, RoutedEventArgs e)
    {
        var builder = new StrategyBuilderWindow { Owner = this };
        builder.ShowDialog();
    }
}

public sealed class TemplateCardViewModel : INotifyPropertyChanged
{
    public StrategyTemplate Template { get; }

    private string _winRate = "…";
    public string WinRate { get => _winRate; set { _winRate = value; OnPropertyChanged(nameof(WinRate)); } }

    private string _tradeCount = "…";
    public string TradeCount { get => _tradeCount; set { _tradeCount = value; OnPropertyChanged(nameof(TradeCount)); } }

    private string _returnPct = "…";
    public string ReturnPct { get => _returnPct; set { _returnPct = value; OnPropertyChanged(nameof(ReturnPct)); } }

    private Brush _returnColor = Brushes.Gray;
    public Brush ReturnColor { get => _returnColor; set { _returnColor = value; OnPropertyChanged(nameof(ReturnColor)); } }

    public TemplateCardViewModel(StrategyTemplate template) { Template = template; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}