using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

    private string _chartSymbol = "BTC/USDT";
    private string _chartTimeframe = "1h";
    private string _chartDataSource = "binance";

    private List<Bar> _currentBars = new();
    private double? _xMin;
    private double? _xMax;

    private bool _isFullscreen;
    private WindowState _prevState;
    private WindowStyle _prevStyle;
    private ResizeMode _prevResize;

    public MainWindow()
    {
        InitializeComponent();
        SignalsList.ItemsSource = _signals;
        Loaded += async (_, _) => await LoadDashboardAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadDashboardAsync();

    private void OpenBuilderButton_Click(object sender, RoutedEventArgs e) => new StrategyBuilderWindow().ShowDialog();
    private void OpenBacktestButton_Click(object sender, RoutedEventArgs e) => new BacktestWindow().ShowDialog();
    private void OpenSettingsButton_Click(object sender, RoutedEventArgs e) => new SettingsWindow().ShowDialog();

    private async void TimeframeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button clicked || clicked.Tag is not string tf) return;

        foreach (var child in TimeframePanel.Children)
        {
            if (child is not Button btn) continue;
            var isSelected = btn == clicked;
            btn.Background = isSelected ? (Brush)FindResource("AccentBg") : SystemColors.ControlBrush;
            btn.Foreground = isSelected ? (Brush)FindResource("AccentText") : SystemColors.ControlTextBrush;
        }

        _chartTimeframe = tf;
        await LoadChartAsync();
    }

    private void FullscreenButton_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isFullscreen) ToggleFullscreen();
    }

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            _prevState = WindowState;
            _prevStyle = WindowStyle;
            _prevResize = ResizeMode;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            _isFullscreen = true;
        }
        else
        {
            WindowStyle = _prevStyle;
            ResizeMode = _prevResize;
            WindowState = _prevState;
            _isFullscreen = false;
        }
    }

    private void ScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        var width = (int)PriceChart.ActualWidth;
        var height = (int)PriceChart.ActualHeight;
        if (width <= 0 || height <= 0) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save chart screenshot",
            Filter = "PNG image (*.png)|*.png",
            FileName = $"Meowgnal_{_chartSymbol.Replace("/", "")}_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        };
        if (dialog.ShowDialog() != true) return;

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(PriceChart);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var stream = File.Create(dialog.FileName);
        encoder.Save(stream);
    }

    // TradingView-style wheel behavior: plain scroll pans left/right,
    // Ctrl+scroll zooms in/out toward the mouse cursor's position.
    private void PriceChart_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_xMin is null || _xMax is null || _currentBars.Count == 0) return;
        e.Handled = true;

        var range = _xMax.Value - _xMin.Value;

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            var chartWidth = PriceChart.ActualWidth;
            if (chartWidth <= 0) return;
            var fraction = Math.Clamp(e.GetPosition(PriceChart).X / chartWidth, 0, 1);
            var mouseValue = _xMin.Value + fraction * range;

            var zoomFactor = e.Delta > 0 ? 0.9 : 1.1111;
            var newRange = range * zoomFactor;
            _xMin = mouseValue - fraction * newRange;
            _xMax = mouseValue + (1 - fraction) * newRange;
        }
        else
        {
            var panStep = range * 0.1 * (e.Delta > 0 ? -1 : 1);
            _xMin += panStep;
            _xMax += panStep;
        }

        ApplyAxes();
    }

    private async Task LoadChartAsync()
    {
        IDataProvider provider = _chartDataSource == "hyperliquid" ? new HyperliquidDataProvider() : new BinanceDataProvider();
        var bars = await provider.GetHistoricalCandlesAsync(_chartSymbol, _chartTimeframe, limit: 200);
        UpdateChart(bars);
        SymbolText.Text = _chartSymbol;
        PriceText.Text = bars[^1].Close.ToString("N2");
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

        _chartSymbol = strategies[0].Symbol;
        _chartTimeframe = strategies[0].Timeframe;
        _chartDataSource = strategies[0].DataSource;
        await LoadChartAsync();

        var allSignals = new List<(SignalDisplayItem Item, DateTime Time)>();
        var totalWinRate = 0.0;
        var totalSignalCount = 0;

        foreach (var strategy in strategies)
        {
            IDataProvider provider = strategy.DataSource == "hyperliquid" ? new HyperliquidDataProvider() : new BinanceDataProvider();
            var bars = await provider.GetHistoricalCandlesAsync(strategy.Symbol, strategy.Timeframe, limit: 200);

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

        WinRateText.Text = $"{totalWinRate / strategies.Count:N0}%";
        SignalCountText.Text = totalSignalCount.ToString();

        foreach (var (item, _) in allSignals.OrderByDescending(x => x.Time).Take(15))
            _signals.Add(item);
    }

    private void UpdateChart(List<Bar> bars)
    {
        _currentBars = bars;
        _xMin = (double)bars.First().Timestamp.Ticks;
        _xMax = (double)bars.Last().Timestamp.Ticks;

        var last = bars[^1];
        var prev = bars.Count > 1 ? bars[^2] : last;

        OhlcOpenText.Text = last.Open.ToString("N2");
        OhlcHighText.Text = last.High.ToString("N2");
        OhlcLowText.Text = last.Low.ToString("N2");
        OhlcCloseText.Text = last.Close.ToString("N2");

        OhlcOpenText.Foreground = last.Open >= prev.Open ? Brushes.MediumSeaGreen : Brushes.IndianRed;
        OhlcHighText.Foreground = last.High >= prev.High ? Brushes.MediumSeaGreen : Brushes.IndianRed;
        OhlcLowText.Foreground = last.Low >= prev.Low ? Brushes.MediumSeaGreen : Brushes.IndianRed;
        OhlcCloseText.Foreground = last.Close >= prev.Close ? Brushes.MediumSeaGreen : Brushes.IndianRed;

        PriceChart.Series = new ISeries[]
        {
            new CandlesticksSeries<FinancialPoint>
            {
                Values = new ObservableCollection<FinancialPoint>(
                    bars.Select(b => new FinancialPoint(b.Timestamp, (double)b.High, (double)b.Open, (double)b.Close, (double)b.Low))),
                UpFill = new SolidColorPaint(new SKColor(0x26, 0xA6, 0x9A)),
                UpStroke = new SolidColorPaint(new SKColor(0x26, 0xA6, 0x9A)) { StrokeThickness = 1 },
                DownFill = new SolidColorPaint(new SKColor(0xEF, 0x53, 0x50)),
                DownStroke = new SolidColorPaint(new SKColor(0xEF, 0x53, 0x50)) { StrokeThickness = 1 },
            }
        };

        ApplyAxes();
    }

    // Recomputes both axes: X uses the current pan/zoom window, Y auto-scales
    // to only the candles currently visible in that window (like TradingView).
    private void ApplyAxes()
    {
        if (_xMin is null || _xMax is null) return;

        var visibleBars = _currentBars
            .Where(b => b.Timestamp.Ticks >= _xMin && b.Timestamp.Ticks <= _xMax)
            .ToList();

        double? yMin = null, yMax = null;
        if (visibleBars.Count > 0)
        {
            var low = (double)visibleBars.Min(b => b.Low);
            var high = (double)visibleBars.Max(b => b.High);
            var padding = (high - low) * 0.08;
            yMin = low - padding;
            yMax = high + padding;
        }

        PriceChart.XAxes = new[]
        {
            new Axis
            {
                Labeler = value => new DateTime((long)value).ToString("MM/dd HH:mm"),
                MinLimit = _xMin,
                MaxLimit = _xMax,
                LabelsPaint = new SolidColorPaint(new SKColor(0x8A, 0x8F, 0x9C)),
            }
        };

        PriceChart.YAxes = new[]
        {
            new Axis
            {
                MinLimit = yMin,
                MaxLimit = yMax,
                Position = LiveChartsCore.Measure.AxisPosition.End,
                LabelsPaint = new SolidColorPaint(new SKColor(0x8A, 0x8F, 0x9C)),
            }
        };
    }
}