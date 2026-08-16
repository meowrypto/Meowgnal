using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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

    // Opens the window with a pre-computed result (used by StrategyBuilderWindow's quick test).
    public BacktestWindow(StrategyDefinition strategy, BacktestResult result) : this()
    {
        Title = $"Backtest: {strategy.Name}";
        TitleText.Text = Title;
        OosHeader.Visibility = Visibility.Collapsed;
        OosCards.Visibility = Visibility.Collapsed;
        OverfitWarning.Visibility = Visibility.Collapsed;

        UpdateInSampleCards(result);
        RenderChart(result);
        TradesGrid.ItemsSource = new ObservableCollection<BacktestTrade>(result.Trades);
        MonthlyGrid.ItemsSource = new ObservableCollection<MonthlyPerformance>(result.MonthlyBreakdown);
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

            _lastResult = wfResult.AggregateOutOfSample;
            RenderChart(wfResult.AggregateOutOfSample);
            TradesGrid.ItemsSource = new ObservableCollection<BacktestTrade>(wfResult.AggregateOutOfSample.Trades);
            MonthlyGrid.ItemsSource = new ObservableCollection<MonthlyPerformance>(wfResult.AggregateOutOfSample.MonthlyBreakdown);
        }
        else
        {
            var result = BacktestEngine.Run(strategy, bars, balance, fee, slippage);
            _lastResult = result;

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

        UpdateSampleSizeBanner(result);
    }

    private void UpdateSampleSizeBanner(BacktestResult result)
    {
        var n = result.Trades.Count;
        switch (result.SampleSizeWarning)
        {
            case "low":
                SampleSizeWarningBanner.Visibility = Visibility.Visible;
                SampleSizeWarningBanner.Background = new SolidColorBrush(Color.FromRgb(0x3D, 0x1A, 0x1A));
                SampleSizeWarningTitle.Text = "⚠️ LOW SAMPLE SIZE";
                SampleSizeWarningTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
                SampleSizeWarningText.Text = $"Only {n} trades — this result is not statistically reliable. At least 30 trades are recommended before trusting a win rate.";
                SampleSizeWarningText.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xE5, 0xE5));
                SampleSizeReliableBanner.Visibility = Visibility.Collapsed;
                break;
            case "moderate":
                SampleSizeWarningBanner.Visibility = Visibility.Visible;
                SampleSizeWarningBanner.Background = new SolidColorBrush(Color.FromRgb(0x4A, 0x3B, 0x10));
                SampleSizeWarningTitle.Text = "⚡ MODERATE SAMPLE";
                SampleSizeWarningTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD9, 0x66));
                SampleSizeWarningText.Text = $"{n} trades — a decent sample, but more data (100+) would give higher confidence.";
                SampleSizeWarningText.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE0));
                SampleSizeReliableBanner.Visibility = Visibility.Collapsed;
                break;
            default:
                SampleSizeWarningBanner.Visibility = Visibility.Collapsed;
                SampleSizeReliableBanner.Visibility = Visibility.Visible;
                SampleSizeReliableText.Text = $"Statistically reliable sample ({n} trades).";
                SampleSizeReliableText.Foreground = new SolidColorBrush(Color.FromRgb(0x7C, 0xE8, 0xA0));
                break;
        }
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

        // Read theme colors and convert to SKColor for LiveCharts
        var upColor = ToSkColor("Up");
        var textMuted = ToSkColor("TextMuted");
        var gridColor = ToSkColor("BorderColor");

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

    // Converts a WPF brush resource key (like "Up" or "TextMuted") to an SKColor for LiveCharts.
    private SKColor ToSkColor(string brushKey)
    {
        if (FindResource(brushKey) is SolidColorBrush brush)
        {
            var c = brush.Color;
            return new SKColor(c.R, c.G, c.B, c.A);
        }
        return SKColors.Gray;
    }
    private void MonteCarlo_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult is null || _lastResult.Trades.Count == 0)
        {
            NotificationService.ShowToast("Meowgnal", "Run a backtest first, then open Monte Carlo analysis.");
            return;
        }
        var strategy = StrategyCombo.SelectedItem as StrategyDefinition;
        var win = new MonteCarloWindow(_lastResult, strategy) { Owner = this };
        win.ShowDialog();
    }

    private void RiskOfRuin_Click(object sender, RoutedEventArgs e)
    {
        var strategy = StrategyCombo.SelectedItem as StrategyDefinition;
        var win = new RiskOfRuinWindow(_lastResult, strategy) { Owner = this };
        win.ShowDialog();
    }

    private BacktestResult? _lastResult;
}