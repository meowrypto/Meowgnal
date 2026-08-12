using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Meowgnal.DataProviders;
using Meowgnal.Engine;
using Meowgnal.Models;
using Meowgnal.Services;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;

namespace Meowgnal.Views;

[SupportedOSPlatform("windows")]
public partial class BacktestWindow : Window
{
    public BacktestWindow()
    {
        InitializeComponent();
        var strategies = StrategyStorageService.LoadAll();
        StrategyCombo.ItemsSource = strategies;
        if (strategies.Count > 0) StrategyCombo.SelectedIndex = 0;
    }

    private void WalkForwardCheck_Changed(object sender, RoutedEventArgs e)
    {
        var isVisible = WalkForwardCheck.IsChecked == true;
        WindowsBox.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        OosPercentBox.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        WindowsLabel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        OosLabel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

        // Reset OOS UI when toggling off
        if (!isVisible)
        {
            OosHeader.Visibility = Visibility.Collapsed;
            OosCards.Visibility = Visibility.Collapsed;
            OverfitWarning.Visibility = Visibility.Collapsed;
        }
    }

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        if (StrategyCombo.SelectedItem is not StrategyDefinition strategy) return;
        IDataProvider provider = strategy.DataSource == "hyperliquid"
            ? new HyperliquidDataProvider()
            : new BinanceDataProvider();

        var bars = await provider.GetHistoricalCandlesAsync(strategy.Symbol, strategy.Timeframe, limit: 2000);
        var balance = decimal.TryParse(BalanceBox.Text, out var b) ? b : 10000m;
        var fee = decimal.TryParse(FeeBox.Text, out var f) ? f : 0.1m;
        var slippage = decimal.TryParse(SlippageBox.Text, out var s) ? s : 0.05m;

        if (WalkForwardCheck.IsChecked == true)
        {
            var windows = int.TryParse(WindowsBox.Text, out var w) ? w : 5;
            var oosPercent = double.TryParse(OosPercentBox.Text, out var o) ? o : 20;

            var wfResult = BacktestEngine.RunWalkForward(strategy, bars, balance, fee, slippage, windows, oosPercent);

            // Update In-Sample Cards
            UpdateCards(wfResult.AggregateInSample, WinRateText, AvgRRText, DrawdownText, TradesText, BalanceResultText);

            // Update Out-of-Sample Cards
            OosHeader.Visibility = Visibility.Visible;
            OosCards.Visibility = Visibility.Visible;
            UpdateCards(wfResult.AggregateOutOfSample, OosWinRateText, OosAvgRRText, OosDrawdownText, OosTradesText, OosBalanceResultText);

            // Show warning if overfit
            if (wfResult.IsOverfit)
            {
                OverfitWarning.Visibility = Visibility.Visible;
                OverfitText.Text = wfResult.OverfitReason;
            }
            else
            {
                OverfitWarning.Visibility = Visibility.Collapsed;
            }

            // Show OOS equity curve as it represents the "real" performance
            RenderChart(wfResult.AggregateOutOfSample);
            TradesGrid.ItemsSource = new ObservableCollection<BacktestTrade>(wfResult.AggregateOutOfSample.Trades);
        }
        else
        {
            var result = BacktestEngine.Run(strategy, bars, balance, fee, slippage);

            // Hide OOS UI in normal mode
            OosHeader.Visibility = Visibility.Collapsed;
            OosCards.Visibility = Visibility.Collapsed;
            OverfitWarning.Visibility = Visibility.Collapsed;

            UpdateCards(result, WinRateText, AvgRRText, DrawdownText, TradesText, BalanceResultText);
            RenderChart(result);
            TradesGrid.ItemsSource = new ObservableCollection<BacktestTrade>(result.Trades);
        }
    }

    private void UpdateCards(BacktestResult result, TextBlock winRate, TextBlock avgRr, TextBlock drawdown, TextBlock trades, TextBlock balance)
    {
        winRate.Text = $"{result.WinRatePercent:N1}%";
        avgRr.Text = result.AverageRiskReward.ToString("N2");
        drawdown.Text = $"{result.MaxDrawdownPercent:N1}%";
        trades.Text = result.Trades.Count.ToString();
        balance.Text = $"{result.StartingBalance:N0} → {result.FinalBalance:N0}";
    }

    private void RenderChart(BacktestResult result)
    {
        var points = result.EquityCurve.Select(p => new ObservablePoint(p.Item1.Ticks, (double)p.Item2)).ToList();

        // TradingView palette colors
        var upColor = new SKColor(0x08, 0x99, 0x81);       // #089981 (TradingView Up)
        var textMuted = new SKColor(0x78, 0x7B, 0x86);    // #787B86 (TradingView muted)
        var gridColor = new SKColor(0x2A, 0x2E, 0x39);    // #2A2E39 (TradingView border)

        EquityChart.Series =
        [
            new LineSeries<ObservablePoint>
            {
                Values = new ObservableCollection<ObservablePoint>(points),
                Stroke = new SolidColorPaint(upColor) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 0
            }
        ];

        EquityChart.XAxes =
        [
            new Axis
            {
                Labeler = value => new DateTime((long)value).ToString("MM/dd HH:mm"),
                LabelsPaint = new SolidColorPaint(textMuted),
                SeparatorsPaint = new SolidColorPaint(gridColor),
                TextSize = 11
            }
        ];

        EquityChart.YAxes =
        [
            new Axis
            {
                LabelsPaint = new SolidColorPaint(textMuted),
                SeparatorsPaint = new SolidColorPaint(gridColor),
                TextSize = 11
            }
        ];
    }
}