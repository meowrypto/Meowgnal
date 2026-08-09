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
using System.Windows.Threading;
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

    // Background signal monitor: periodically rescans all strategies and
    // raises toast/sound alerts only for signals it hasn't seen before.
    private DispatcherTimer? _monitorTimer;
    private readonly HashSet<string> _knownSignalKeys = new();
    private bool _baselineSeeded;
    private bool _isScanning;

    // Live UTC clock in the bottom status bar (TradingView style).
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    // Exact TradingView up/down colors for the OHLC legend.
    private static readonly SolidColorBrush UpBrush = new(Color.FromRgb(0x08, 0x99, 0x81));
    private static readonly SolidColorBrush DownBrush = new(Color.FromRgb(0xF2, 0x36, 0x45));

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
        ChartWebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0x13, 0x17, 0x22);

        // Default chart type: candlestick.
        ChartTypeCombo.SelectedIndex = 0;

        // Live UTC clock.
        UtcClockText.Text = DateTime.UtcNow.ToString("HH:mm:ss");
        _clockTimer.Tick += (_, _) => UtcClockText.Text = DateTime.UtcNow.ToString("HH:mm:ss");
        _clockTimer.Start();

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _ = InitializeChartWebViewAsync();
        await LoadDashboardAsync();
        StartSignalMonitor();
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

            // JavaScript -> C# side of the bridge (crosshair OHLC updates).
            core.WebMessageReceived += OnChartWebMessageReceived;

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

    // JavaScript -> C# message handler. The chart page reports which candle
    // the mouse is hovering over; we mirror its OHLC into the header bar,
    // coloring each value against the previous candle (like TradingView).
    private void OnChartWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // Some WebView2 runtime versions hand us the message as a quoted
        // JSON string instead of an object — unwrap one level so both work.
        var json = e.WebMessageAsJson;
        using (var probe = JsonDocument.Parse(json))
        {
            if (probe.RootElement.ValueKind == JsonValueKind.String)
                json = probe.RootElement.GetString()!;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "crosshair") return;

        if (root.TryGetProperty("hasData", out var hasData) && hasData.GetBoolean())
        {
            SetOhlcHeader(
                (decimal)root.GetProperty("open").GetDouble(),
                (decimal)root.GetProperty("high").GetDouble(),
                (decimal)root.GetProperty("low").GetDouble(),
                (decimal)root.GetProperty("close").GetDouble(),
                (decimal)root.GetProperty("prevOpen").GetDouble(),
                (decimal)root.GetProperty("prevHigh").GetDouble(),
                (decimal)root.GetProperty("prevLow").GetDouble(),
                (decimal)root.GetProperty("prevClose").GetDouble());
        }
        else if (_currentBars.Count > 0)
        {
            // Mouse left the chart: show the newest candle again.
            var last = _currentBars[^1];
            var prev = _currentBars.Count > 1 ? _currentBars[^2] : last;
            SetOhlcHeader(last.Open, last.High, last.Low, last.Close, prev.Open, prev.High, prev.Low, prev.Close);
        }
    }

    // Writes the four OHLC values into the header bar, coloring each one
    // green/red (TradingView palette) by comparing it with the previous candle.
    private void SetOhlcHeader(
        decimal open, decimal high, decimal low, decimal close,
        decimal prevOpen, decimal prevHigh, decimal prevLow, decimal prevClose)
    {
        OhlcOpenText.Text = open.ToString("N2");
        OhlcHighText.Text = high.ToString("N2");
        OhlcLowText.Text = low.ToString("N2");
        OhlcCloseText.Text = close.ToString("N2");

        OhlcOpenText.Foreground = open >= prevOpen ? UpBrush : DownBrush;
        OhlcHighText.Foreground = high >= prevHigh ? UpBrush : DownBrush;
        OhlcLowText.Foreground = low >= prevLow ? UpBrush : DownBrush;
        OhlcCloseText.Foreground = close >= prevClose ? UpBrush : DownBrush;
    }

    // Opens tradingview.com in the default browser. This visible attribution
    // link is required by the Lightweight Charts license.
    private void TradingViewLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadDashboardAsync();

    private void OpenBuilderButton_Click(object sender, RoutedEventArgs e) => new StrategyBuilderWindow().ShowDialog();

    private void OpenBacktestButton_Click(object sender, RoutedEventArgs e) => new BacktestWindow().ShowDialog();

    private void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow().ShowDialog();
        StartSignalMonitor(); // apply a changed check interval immediately
    }

    private async void TimeframeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button clicked || clicked.Tag is not string tf) return;

        foreach (var child in TimeframePanel.Children)
        {
            if (child is not Button btn) continue;
            var isSelected = btn == clicked;
            btn.Background = isSelected ? (Brush)FindResource("Accent") : Brushes.Transparent;
            btn.Foreground = isSelected ? Brushes.White : (Brush)FindResource("TextSecondary");
        }

        _chartTimeframe = tf;
        await LoadChartAsync();
    }

    // Sends the selected chart type to the page; the page rebuilds the
    // series (candles / line / area / Heikin Ashi / bars) from its raw data.
    private void ChartTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChartTypeCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;
        _ = SendChartTypeAsync(tag);
    }

    private async Task SendChartTypeAsync(string chartType)
    {
        try
        {
            await _chartPageReady.Task;
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (ChartWebView.CoreWebView2 is null) return;
        ChartWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "setChartType", chartType }));
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
        var bars = await provider.GetHistoricalCandlesAsync(_chartSymbol, _chartTimeframe, limit: 1000);
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
            var bars = await provider.GetHistoricalCandlesAsync(strategy.Symbol, strategy.Timeframe, limit: 500);
            var signals = RuleEngine.ScanForSignals(strategy, bars);
            var backtest = BacktestEngine.Run(strategy, bars, startingBalance: 10000m, feePercent: 0.1m, slippagePercent: 0.05m);

            totalWinRate += backtest.WinRatePercent;
            totalSignalCount += signals.Count;

            foreach (var s in signals)
            {
                // Remember everything that already exists so the background
                // monitor never toasts for old signals.
                _knownSignalKeys.Add(MakeSignalKey(strategy.StrategyId, s));

                allSignals.Add((new SignalDisplayItem
                {
                    Symbol = strategy.Symbol,
                    Description = strategy.Name,
                    Type = s.Type == SignalType.Entry ? "buy" : "sell",
                    Time = s.Timestamp.ToString("g")
                }, s.Timestamp));
            }
        }

        _baselineSeeded = true;

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
        SetOhlcHeader(last.Open, last.High, last.Low, last.Close, prev.Open, prev.High, prev.Low, prev.Close);

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
                close = b.Close,
                volume = b.Volume
            }).ToArray()
        };

        ChartWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
    }

    // ------------------------------------------------------------------
    // Background signal monitor
    // ------------------------------------------------------------------

    private sealed record FoundSignal(StrategyDefinition Strategy, SignalEvent Signal);

    private static string MakeSignalKey(string strategyId, SignalEvent signal) =>
        $"{strategyId}|{signal.Timestamp:O}|{(int)signal.Type}";

    // Starts (or re-syncs) the periodic scan timer using the interval stored
    // in Settings -> Notifications (default 60 seconds).
    private void StartSignalMonitor()
    {
        var seconds = Math.Clamp(SettingsStorageService.Load().SignalCheckIntervalSeconds, 10, 3600);
        var interval = TimeSpan.FromSeconds(seconds);

        if (_monitorTimer is null)
        {
            _monitorTimer = new DispatcherTimer { Interval = interval };
            _monitorTimer.Tick += async (_, _) => await MonitorTickAsync();
            _monitorTimer.Start();
        }
        else if (_monitorTimer.Interval != interval)
        {
            _monitorTimer.Interval = interval;
        }
    }

    private async Task MonitorTickAsync()
    {
        if (_isScanning) return; // previous scan still running on a slow network
        _isScanning = true;
        try
        {
            var settings = SettingsStorageService.Load();
            var found = await ScanAllStrategiesAsync();

            if (!_baselineSeeded)
            {
                // First scan after startup with no strategies loaded yet:
                // silently remember everything so we never toast old signals.
                _baselineSeeded = true;
                foreach (var f in found) _knownSignalKeys.Add(MakeSignalKey(f.Strategy.StrategyId, f.Signal));
                return;
            }

            var fresh = found
                .Where(f => !_knownSignalKeys.Contains(MakeSignalKey(f.Strategy.StrategyId, f.Signal)))
                .ToList();
            if (fresh.Count == 0) return;

            foreach (var f in fresh)
            {
                _knownSignalKeys.Add(MakeSignalKey(f.Strategy.StrategyId, f.Signal));
                _signals.Insert(0, new SignalDisplayItem
                {
                    Symbol = f.Strategy.Symbol,
                    Description = f.Strategy.Name,
                    Type = f.Signal.Type == SignalType.Entry ? "buy" : "sell",
                    Time = f.Signal.Timestamp.ToString("g")
                });
            }

            while (_signals.Count > 30) _signals.RemoveAt(_signals.Count - 1);
            SignalCountText.Text = found.Count.ToString();

            if (settings.ToastNotificationsEnabled)
            {
                foreach (var f in fresh.Take(5)) // avoid a toast flood
                {
                    NotificationService.ShowToast(
                        $"Meowgnal — {f.Strategy.Symbol} ({f.Strategy.Timeframe})",
                        $"{(f.Signal.Type == SignalType.Entry ? "BUY" : "SELL")} signal: {f.Strategy.Name}");
                }
            }

            if (settings.SoundNotificationsEnabled)
                NotificationService.PlayAlertSound();
        }
        catch
        {
            // A failed scan tick must never crash the app; retry next tick.
        }
        finally
        {
            _isScanning = false;
        }
    }

    private static async Task<List<FoundSignal>> ScanAllStrategiesAsync()
    {
        var found = new List<FoundSignal>();
        foreach (var strategy in StrategyStorageService.LoadAll())
        {
            try
            {
                IDataProvider provider = strategy.DataSource == "hyperliquid" ? new HyperliquidDataProvider() : new BinanceDataProvider();
                var bars = await provider.GetHistoricalCandlesAsync(strategy.Symbol, strategy.Timeframe, limit: 500);
                foreach (var signal in RuleEngine.ScanForSignals(strategy, bars))
                    found.Add(new FoundSignal(strategy, signal));
            }
            catch
            {
                // Skip strategies whose exchange is unreachable this tick.
            }
        }
        return found;
    }
}