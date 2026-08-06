using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Meowgnal.DataProviders;
using Meowgnal.Engine;
using Meowgnal.Models;

namespace Meowgnal;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<SignalDisplayItem> _signals = new();

    public MainWindow()
    {
        InitializeComponent();
        SignalsList.ItemsSource = _signals;
        Loaded += async (_, _) => await LoadDashboardAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadDashboardAsync();

    private async System.Threading.Tasks.Task LoadDashboardAsync()
    {
        var provider = new BinanceDataProvider();
        var bars = await provider.GetHistoricalCandlesAsync("BTC/USDT", "1h", limit: 200);
        PriceText.Text = bars[^1].Close.ToString("N2");

        var strategy = new StrategyDefinition
        {
            Name = "EMA Cross + RSI Filter",
            Symbol = "BTC/USDT",
            Timeframe = "1h",
            Indicators =
            {
                new IndicatorDefinition { Id = "ema9", Type = "EMA", Params = new() { ["period"] = 9 } },
                new IndicatorDefinition { Id = "ema21", Type = "EMA", Params = new() { ["period"] = 21 } },
                new IndicatorDefinition { Id = "rsi14", Type = "RSI", Params = new() { ["period"] = 14 } },
            },
            EntryRules = new RuleGroup
            {
                Mode = "threshold",
                MinScore = 3,
                TriggerMode = "onTransition",
                Conditions =
                {
                    new LeafCondition { Left = "ema9", Op = "crossesAbove", Right = "ema21", Weight = 2 },
                    new LeafCondition { Left = "rsi14", Op = "lessThan", Right = 70.0, Weight = 1 },
                }
            },
            ExitRules = new RuleGroup
            {
                Mode = "any",
                Conditions = { new LeafCondition { Left = "ema9", Op = "crossesBelow", Right = "ema21" } }
            }
        };

        var signals = RuleEngine.ScanForSignals(strategy, bars);
        var backtest = BacktestEngine.Run(strategy, bars, startingBalance: 10000m, feePercent: 0.1m, slippagePercent: 0.05m);

        WinRateText.Text = $"{backtest.WinRatePercent:N0}%";
        SignalCountText.Text = signals.Count.ToString();

        _signals.Clear();
        foreach (var s in signals.OrderByDescending(s => s.Timestamp).Take(10))
        {
            _signals.Add(new SignalDisplayItem
            {
                Symbol = strategy.Symbol,
                Description = strategy.Name,
                Type = s.Type == SignalType.Entry ? "buy" : "sell",
                Time = s.Timestamp.ToString("g")
            });
        }
    }
}