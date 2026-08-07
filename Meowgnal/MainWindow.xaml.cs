using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Meowgnal.DataProviders;
using Meowgnal.Engine;
using Meowgnal.Models;
using Meowgnal.Services;
using Meowgnal.Views;

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

    private void OpenBuilderButton_Click(object sender, RoutedEventArgs e)
    {
        var builder = new StrategyBuilderWindow();
        builder.ShowDialog();
    }

    private async Task LoadDashboardAsync()
    {
        var strategies = StrategyStorageService.LoadAll();
        _signals.Clear();
        ActiveStrategiesText.Text = strategies.Count.ToString();

        if (strategies.Count == 0)
        {
            SymbolText.Text = "No strategies yet";
            PriceText.Text = "";
            WinRateText.Text = "—";
            SignalCountText.Text = "0";
            return;
        }

        var allSignals = new List<(SignalDisplayItem Item, DateTime Time)>();
        List<Bar>? chartBars = null;
        var totalWinRate = 0.0;
        var totalSignalCount = 0;

        foreach (var strategy in strategies)
        {
            IDataProvider provider = strategy.DataSource == "hyperliquid"
                ? new HyperliquidDataProvider()
                : new BinanceDataProvider();

            var bars = await provider.GetHistoricalCandlesAsync(strategy.Symbol, strategy.Timeframe, limit: 200);
            chartBars ??= bars; // dashboard chart shows the first strategy's symbol for now

            var signals = RuleEngine.ScanForSignals(strategy, bars);
            var backtest = BacktestEngine.Run(strategy, bars, startingBalance: 10000m, feePercent: 0.1m, slippagePercent: 0.05m);

            totalWinRate += backtest.WinRatePercent;
            totalSignalCount += signals.Count;

            foreach (var s in signals)
            {
                allSignals.Add((new SignalDisplayItem
                {
                    Symbol = strategy.Symbol,
                    Description = strategy.Name,
                    Type = s.Type == SignalType.Entry ? "buy" : "sell",
                    Time = s.Timestamp.ToString("g")
                }, s.Timestamp));
            }
        }

        if (chartBars is not null)
        {
            UpdateChart(chartBars);
            SymbolText.Text = strategies[0].Symbol;
            PriceText.Text = chartBars[^1].Close.ToString("N2");
        }

        WinRateText.Text = $"{totalWinRate / strategies.Count:N0}%";
        SignalCountText.Text = totalSignalCount.ToString();

        foreach (var (item, _) in allSignals.OrderByDescending(x => x.Time).Take(15))
            _signals.Add(item);
    }

    private void UpdateChart(List<Bar> bars)
    {
        var points = bars.Select(b => new FinancialPoint(b.Timestamp, (double)b.High, (double)b.Open, (double)b.Close, (double)b.Low));

        PriceChart.Series = new ISeries[]
        {
            new CandlesticksSeries<FinancialPoint>
            {
                Values = new ObservableCollection<FinancialPoint>(points),
                UpFill = new SolidColorPaint(new SKColor(0x26, 0xA6, 0x9A)),
                UpStroke = new SolidColorPaint(new SKColor(0x26, 0xA6, 0x9A)) { StrokeThickness = 1 },
                DownFill = new SolidColorPaint(new SKColor(0xEF, 0x53, 0x50)),
                DownStroke = new SolidColorPaint(new SKColor(0xEF, 0x53, 0x50)) { StrokeThickness = 1 },
            }
        };

        PriceChart.XAxes = new[]
        {
            new Axis
            {
                Labeler = value => new DateTime((long)value).ToString("MM/dd HH:mm"),
                UnitWidth = TimeSpan.FromHours(1).Ticks,
                LabelsPaint = new SolidColorPaint(new SKColor(0x8A, 0x8F, 0x9C)),
            }
        };

        PriceChart.YAxes = new[]
        {
            new Axis { LabelsPaint = new SolidColorPaint(new SKColor(0x8A, 0x8F, 0x9C)) }
        };
    }
    private void OpenBacktestButton_Click(object sender, RoutedEventArgs e)
    {
        var backtest = new BacktestWindow();
        backtest.ShowDialog();
    }
    private void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = new SettingsWindow();
        settings.ShowDialog();
    }
}