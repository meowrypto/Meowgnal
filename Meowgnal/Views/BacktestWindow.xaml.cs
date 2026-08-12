using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Versioning;
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

            UpdateInSampleCards(wfResult.AggregateInSample);

            OosHeader.Visibility = Visibility.Visible;
            OosCards.Visibility = Visibility.Visible;
            UpdateOutOfSampleCards(wfResult.AggregateOutOfSample);

            if (wfResult.IsOverfit)
            {
                OverfitWarning.Visibility = Visibility.Visible;
                OverfitText.Text = wfResult.OverfitReason;
            }
            else
            {
                OverfitWarning.Visibility = Visibility.Collapsed;
            }

            RenderChart(wfResult.AggregateOutOfSample);
            TradesGrid.ItemsSource = new ObservableCollection<BacktestTrade>(wfResult.AggregateOutOfSample.Trades);
            MonthlyGrid.ItemsSource = new ObservableCollection<MonthlyPerformance>(wfResult.AggregateOutOfSample.MonthlyBreakdown);
        }
        else
        {
            var result = BacktestEngine.Run(strategy, bars, balance, fee, slippage);

            OosHeader.Visibility = Visibility.Collapsed;
            OosCards.Visibility = Visibility.Collapsed;
            OverfitWarning.Visibility = Visibility.Collapsed;

            UpdateInSampleCards(result);
            RenderChart(result);
            TradesGrid.ItemsSource = new ObservableCollection<BacktestTrade>(result.Trades);
            MonthlyGrid.ItemsSource = new ObservableCollection<MonthlyPerformance>(result.MonthlyBreakdown);
        }
    }

    private void UpdateInSampleCards(BacktestResult result)
    {
        WinRateText.Text = $"{result.WinRatePercent:N1}%";
        AvgRRText.Text = result.AverageRiskReward.ToString("N2");
        DrawdownText.Text = $"{result.MaxDrawdownPercent:N1}%";
        TradesText.Text = result.Trades.Count.ToString();
        BalanceResultText.Text = $"{result.StartingBalance:N0} → {result.FinalBalance:N0}";
        SharpeText.Text = result.SharpeRatio.ToString("N2");
        SortinoText.Text = result.SortinoRatio.ToString("N2");
    }

    private void UpdateOutOfSampleCards(BacktestResult result)
    {
        OosWinRateText.Text = $"{result.WinRatePercent:N1}%";
        OosAvgRRText.Text = result.AverageRiskReward.ToString("N2");
        OosDrawdownText.Text = $"{result.MaxDrawdownPercent:N1}%";
        OosTradesText.Text = result.Trades.Count.ToString();
        OosBalanceResultText.Text = $"{result.StartingBalance:N0} → {result.FinalBalance:N0}";
        OosSharpeText.Text = result.SharpeRatio.ToString("N2");
        OosSortinoText.Text = result.SortinoRatio.ToString("N2");
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