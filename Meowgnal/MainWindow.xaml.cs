using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Meowgnal.DataProviders;
using Meowgnal.Engine;
using Meowgnal.Models;
using Meowgnal.Services;
using Meowgnal.Views;

namespace Meowgnal;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<SignalDisplayItem> _signals = new();

    // Completes once chart.html has fully loaded inside the WebView2.
    // Candle data sent before that moment simply waits, so we never post
    // messages into a page that isn't ready yet.
    private readonly TaskCompletionSource<bool> _chartPageReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private string _chartSymbol = "BTC/USDT";
    private string _chartTimeframe = "1h";
    private string _chartDataSource = "binance";
    private List<Bar> _currentBars = new();
    private bool _isFullscreen;
    private WindowState _prevState;
    private WindowStyle _prevStyle;
    private ResizeMode _prevResize;

    public MainWindow()
    {
        InitializeComponent();
        SignalsList.ItemsSource = _signals;

        // Matches the chart page background so there is no white flash
        // while the WebView2 is starting up.
        ChartWebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0x0B, 0x0D, 0x12);

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _ = InitializeChartWebViewAsync();
        await LoadDashboardAsync();
    }

    // Starts the embedded browser, points it at our local ChartHost folder
    // (served under a virtual https host) and loads chart.html.
    private async Task InitializeChartWebViewAsync()
    {
        try
        {
            await ChartWebView.EnsureCoreWebView2Async();

            var core = ChartWebView.CoreWebView2;

            // App-like feel: no browser chrome, no right-click menu,
            // no page zoom (the chart library handles zoom/pan itself).
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;

            core.NavigationCompleted += (_, _) => _chartPageReady.TrySetResult(true);

            var hostFolder = Path.Combine(AppContext.BaseDirectory, "ChartHost");
            core.SetVirtualHostNameToFolderMapping("meowgnal.local", hostFolder, CoreWebView2HostResourceAccessKind.Allow);
            core.Navigate("https://meowgnal.local/chart.html");
        }
        catch (WebView2RuntimeNotFoundException)
        {
            _chartPageReady.TrySetCanceled();
            MessageBox.Show(
                "The chart engine (WebView2 Runtime) is not installed on this system.\n" +
                "Please download and install this small official package from Microsoft, then run the app again:\n\n" +
                "https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                "Meowgnal — chart engine missing",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            _chartPageReady.TrySetCanceled();
            MessageBox.Show(
                "The chart could not be initialized:\n" + ex.Message,
                "Meowgnal",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
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

    // WebView2 captures its own web content directly — this is the only
    // reliable way to screenshot the chart now (the old WPF render method
    // would produce a blank image for web content).
    private async void ScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        if (ChartWebView.CoreWebView2 is null) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save chart screenshot",
            Filter = "PNG image (*.png)|*.png",
            FileName = $"Meowgnal_{_chartSymbol.Replace("/", "")}_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        };
        if (dialog.ShowDialog() != true) return;

        await using var stream = File.Create(dialog.FileName);
        await ChartWebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
    }

    private async Task LoadChartAsync()
    {
        IDataProvider provider = _chartDataSource == "hyperliquid" ? new HyperliquidDataProvider() : new BinanceDataProvider();
        var bars = await provider.GetHistoricalCandlesAsync(_chartSymbol, _chartTimeframe, limit: 200);
        await UpdateChartAsync(bars);
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

    // Updates the OHLC header (newest candle) and pushes all candles
    // to the TradingView chart running inside the WebView2.
    private async Task UpdateChartAsync(List<Bar> bars)
    {
        _currentBars = bars;

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

        await SendCandlesToChartAsync(bars);
    }

    // The C# -> JavaScript side of the bridge. Sends one JSON message;
    // chart.html listens for it and redraws the chart.
    private async Task SendCandlesToChartAsync(List<Bar> bars)
    {
        try
        {
            await _chartPageReady.Task;
        }
        catch (TaskCanceledException)
        {
            return; // WebView2 runtime was missing — nothing to send to.
        }

        if (ChartWebView.CoreWebView2 is null) return;

        var payload = new
        {
            type = "setCandles",
            data = bars.Select(b => new
            {
                // Lightweight Charts expects UTC Unix time in SECONDS.
                time = new DateTimeOffset(b.Timestamp).ToUnixTimeSeconds(),
                open = b.Open,
                high = b.High,
                low = b.Low,
                close = b.Close
            }).ToArray()
        };

        ChartWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
    }
}