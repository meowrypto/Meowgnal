using System.Linq;
using System.Windows;
using Meowgnal.DataProviders;
using Meowgnal.Engine;
using Meowgnal.Models;

namespace Meowgnal;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        var provider = new BinanceDataProvider();
        var bars = await provider.GetHistoricalCandlesAsync("BTC/USDT", "1h", limit: 5);
        ResultText.Text = $"[Binance] Got {bars.Count} candles.\nLast close: {bars[^1].Close}";
    }

    private async void TestHyperliquidButton_Click(object sender, RoutedEventArgs e)
    {
        var provider = new HyperliquidDataProvider();
        var bars = await provider.GetHistoricalCandlesAsync("BTC/USDT", "1h", limit: 5);
        ResultText.Text = $"[Hyperliquid] Got {bars.Count} candles.\nLast close: {bars[^1].Close}";
    }

    private async void TestIndicatorButton_Click(object sender, RoutedEventArgs e)
    {
        var provider = new BinanceDataProvider();
        var bars = await provider.GetHistoricalCandlesAsync("BTC/USDT", "1h", limit: 100);

        var indicatorDef = new IndicatorDefinition { Id = "ema9", Type = "EMA", Params = new() { ["period"] = 9 } };
        var emaValues = IndicatorEngine.Calculate(bars, indicatorDef);

        ResultText.Text = $"[EMA9] Last close: {bars[^1].Close}\nEMA9: {emaValues.Last():N2}";
    }

    private async void TestStrategyButton_Click(object sender, RoutedEventArgs e)
    {
        var provider = new BinanceDataProvider();
        var bars = await provider.GetHistoricalCandlesAsync("BTC/USDT", "1h", limit: 200);

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
        var entryCount = signals.Count(s => s.Type == SignalType.Entry);
        var exitCount = signals.Count(s => s.Type == SignalType.Exit);
        var last = signals.LastOrDefault();

        ResultText.Text = $"Scanned {bars.Count} candles.\n" +
                           $"Entry signals: {entryCount}, Exit signals: {exitCount}\n" +
                           (last is not null ? $"Last: {last.Type} at {last.Timestamp:g}" : "No signals yet");
    }
}