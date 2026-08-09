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
    // One open chart tab per symbol (TradingView style). Each tab remembers
    // its own symbol, timeframe, chart type and data source.
    private sealed class ChartTab
    {
        public string Symbol { get; set; } = "BTC/USDT";
        public string Timeframe { get; set; } = "1h";
        public string ChartType { get; set; } = "candles";
        public string DataSource { get; set; } = "binance";
    }

    private readonly List<ChartTab> _tabs = new();
    private ChartTab? _activeTab;

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

    // Full timeframe catalog shown in the dropdown menu (TradingView style),
    // from seconds up to months. Also defines the stable sort order.
    private static readonly (string Group, string[] Items)[] TimeframeCatalog =
    {
        ("SECONDS", new[] { "1s", "5s", "15s", "30s" }),
        ("MINUTES", new[] { "1m", "2m", "3m", "5m", "10m", "15m", "30m", "45m" }),
        ("HOURS", new[] { "1h", "2h", "3h", "4h", "6h", "8h", "12h" }),
        ("DAYS", new[] { "1d", "3d" }),
        ("WEEKS / MONTHS", new[] { "1w", "1M" }),
    };

    private static readonly string[] CatalogOrder = TimeframeCatalog.SelectMany(g => g.Items).ToArray();

    private const int MaxFavoriteTimeframes = 6;

    private readonly List<string> _favoriteTfs;
    private readonly HashSet<string> _collapsedGroups = new() { "SECONDS" };

    private string _chartSymbol = "BTC/USDT";
    private string _chartTimeframe = "1h";
    private string _chartDataSource = "binance";
    private string _chartType = "candles";
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

        // Default chart type: candlestick (sets button icon + label).
        ApplyChartType("candles");

        // Starred timeframes (persisted encrypted with the other settings).
        _favoriteTfs = SettingsStorageService.Load().FavoriteTimeframes;
        RebuildTimeframeBar();

        // Live UTC clock.
        UtcClockText.Text = DateTime.UtcNow.ToString("HH:mm:ss");
        _clockTimer.Tick += (_, _) => UtcClockText.Text = DateTime.UtcNow.ToString("HH:mm:ss");
        _clockTimer.Start();

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _ = InitializeChartWebViewAsync();

        // First tab comes from the first saved strategy (or the default pair).
        var first = StrategyStorageService.LoadAll().FirstOrDefault();
        var firstTab = new ChartTab
        {
            Symbol = first?.Symbol ?? "BTC/USDT",
            Timeframe = first?.Timeframe ?? "1h",
            ChartType = "candles",
            DataSource = first?.DataSource ?? SettingsStorageService.Load().DefaultDataSource,
        };
        _tabs.Add(firstTab);
        await ActivateTabAsync(firstTab);

        await LoadDashboardAsync();
        StartSignalMonitor();
    }

    // ------------------------------------------------------------------
    // Chart tabs (one per symbol)
    // ------------------------------------------------------------------

    // Rebuilds the tab strip; the active tab is highlighted.
    private void RebuildTabsBar()
    {
        TabsPanel.Children.Clear();

        foreach (var tab in _tabs)
        {
            var isActive = tab == _activeTab;

            var border = new Border
            {
                Background = isActive ? (Brush)FindResource("BgPanel") : Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 4, 0),
                Padding = new Thickness(10, 5, 10, 5),
                Cursor = Cursors.Hand,
                Tag = tab,
            };
            border.MouseLeftButtonUp += Tab_Click;

            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock
            {
                Text = tab.Symbol,
                Foreground = isActive ? (Brush)FindResource("TextPrimary") : (Brush)FindResource("TextMuted"),
                FontSize = 12,
                FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var close = new TextBlock
            {
                Text = "✕",
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 10,
                Margin = new Thickness(8, 1, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Tag = tab,
            };
            close.MouseLeftButtonUp += TabClose_Click;

            sp.Children.Add(close);
            border.Child = sp;
            TabsPanel.Children.Add(border);
        }
    }

    private async void Tab_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not ChartTab tab || tab == _activeTab) return;
        await ActivateTabAsync(tab);
    }

    private void TabClose_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBlock t || t.Tag is not ChartTab tab) return;
        if (_tabs.Count <= 1) return; // always keep at least one tab open

        _tabs.Remove(tab);
        if (_activeTab == tab)
        {
            _ = ActivateTabAsync(_tabs[0]);
        }
        else
        {
            RebuildTabsBar();
        }
    }

    private void AddTabButton_Click(object sender, RoutedEventArgs e)
    {
        NewTabSymbolBox.Text = "ETH/USDT";
        NewTabPopup.IsOpen = !NewTabPopup.IsOpen;
    }

    private async void NewTabOpen_Click(object sender, RoutedEventArgs e)
    {
        var symbol = NormalizeSymbol(NewTabSymbolBox.Text);
        if (symbol is null)
        {
            MessageBox.Show("Please enter a valid symbol, e.g. ETH/USDT", "Meowgnal",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        NewTabPopup.IsOpen = false;

        // Same symbol already open? Just switch to that tab.
        var existing = _tabs.FirstOrDefault(t => t.Symbol == symbol);
        if (existing is not null)
        {
            await ActivateTabAsync(existing);
            return;
        }

        // Data source: follow a strategy on this symbol if one exists,
        // otherwise fall back to the default in Settings.
        var dataSource = StrategyStorageService.LoadAll().FirstOrDefault(s => s.Symbol == symbol)?.DataSource
                         ?? SettingsStorageService.Load().DefaultDataSource;

        var tab = new ChartTab
        {
            Symbol = symbol,
            Timeframe = "1h",
            ChartType = _chartType,
            DataSource = dataSource,
        };
        _tabs.Add(tab);
        await ActivateTabAsync(tab);
    }

    // "ethusdt" -> "ETH/USDT", "btc/usdt" -> "BTC/USDT", "ETH" -> "ETH/USDT".
    private static string? NormalizeSymbol(string input)
    {
        var s = input.Trim().ToUpperInvariant();
        if (s.Length == 0) return null;
        if (s.Contains('/')) return s;
        if (s.EndsWith("USDT") && s.Length > 4) return s[..^4] + "/USDT";
        if (s.EndsWith("USD") && s.Length > 3) return s[..^3] + "/USD";
        return s + "/USDT";
    }

    // Makes the given tab active and restores all of its saved chart state.
    private async Task ActivateTabAsync(ChartTab tab)
    {
        _activeTab = tab;
        _chartSymbol = tab.Symbol;
        _chartTimeframe = tab.Timeframe;
        _chartDataSource = tab.DataSource;
        _chartType = tab.ChartType;

        RebuildTabsBar();
        RebuildTimeframeBar();
        ApplyChartType(_chartType);
        _ = SendChartTypeAsync(_chartType);

        await LoadChartAsync();
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

    // ------------------------------------------------------------------
    // Timeframe toolbar + full TradingView-style menu with favorites
    // ------------------------------------------------------------------

    // Rebuilds the toolbar buttons in a STABLE order: always smallest to
    // largest interval, left to right — selecting one never moves buttons.
    private void RebuildTimeframeBar()
    {
        TimeframePanel.Children.Clear();

        var toShow = _favoriteTfs
            .Union(new[] { _chartTimeframe })
            .OrderBy(tf => Array.IndexOf(CatalogOrder, tf))
            .ToList();

        foreach (var tf in toShow)
        {
            var btn = new Button
            {
                Content = tf,
                Tag = tf,
                Style = (Style)FindResource("TvButton"),
            };
            btn.Click += TimeframeButton_Click;
            if (tf == _chartTimeframe)
            {
                btn.Background = (Brush)FindResource("Accent");
                btn.Foreground = Brushes.White;
            }
            TimeframePanel.Children.Add(btn);
        }
    }

    private async void TimeframeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tf || tf == _chartTimeframe) return;
        _chartTimeframe = tf;
        if (_activeTab is not null) _activeTab.Timeframe = tf;
        RebuildTimeframeBar();
        await LoadChartAsync();
    }

    private void TimeframeMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (TimeframePopup.IsOpen)
        {
            TimeframePopup.IsOpen = false;
            return;
        }

        BuildTimeframeMenu();
        FitTimeframeMenuToWindow();
        TimeframePopup.IsOpen = true;
    }

    // Keeps the dropdown INSIDE the app window: measures the free space from
    // the button down to the window's own bottom edge. Small window → shorter
    // menu that scrolls inside itself; big/fullscreen window → tall menu.
    private void FitTimeframeMenuToWindow()
    {
        // Both values are in the window's own coordinate space, so the OS
        // window chrome (title bar/borders) can never skew the measurement.
        var buttonBottomY = TimeframeMenuButton.TranslatePoint(
            new Point(0, TimeframeMenuButton.ActualHeight), this).Y;
        var clientBottomY = (Content as FrameworkElement)?.ActualHeight ?? ActualHeight;

        var available = clientBottomY - buttonBottomY - 6;
        TimeframeMenuScroll.MaxHeight = Math.Clamp(available, 160, 640);
    }

    // Fills the dropdown: grouped catalog (seconds → months), each section
    // separated by a line and collapsible via its header arrow (like TV).
    private void BuildTimeframeMenu()
    {
        TimeframeMenuPanel.Children.Clear();

        foreach (var (group, items) in TimeframeCatalog)
        {
            // Separator line between sections.
            if (TimeframeMenuPanel.Children.Count > 0)
            {
                TimeframeMenuPanel.Children.Add(new Border
                {
                    Height = 1,
                    Background = (Brush)FindResource("BorderLine"),
                    Margin = new Thickness(4, 6, 4, 2),
                });
            }

            var isCollapsed = _collapsedGroups.Contains(group);

            // Section header with collapse arrow.
            var header = new Button
            {
                Style = (Style)FindResource("TvButtonLeft"),
                Tag = group,
            };
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            headerPanel.Children.Add(new TextBlock
            {
                Text = group,
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
            });
            headerPanel.Children.Add(new TextBlock
            {
                Text = isCollapsed ? "˅" : "˄",
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 9,
                Margin = new Thickness(6, 1, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            header.Content = headerPanel;
            header.Click += TimeframeGroupHeader_Click;
            TimeframeMenuPanel.Children.Add(header);

            // Collapsible body of the section.
            var groupPanel = new StackPanel
            {
                Visibility = isCollapsed ? Visibility.Collapsed : Visibility.Visible,
            };

            foreach (var tf in items)
            {
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var selectBtn = new Button
                {
                    Content = tf,
                    Tag = tf,
                    Style = (Style)FindResource("TvButtonLeft"),
                };
                selectBtn.Click += TimeframeMenuSelect_Click;
                if (tf == _chartTimeframe) selectBtn.Foreground = (Brush)FindResource("Accent");

                var isFav = _favoriteTfs.Contains(tf);
                var starBtn = new Button
                {
                    Content = isFav ? "★" : "☆",
                    Tag = tf,
                    Style = (Style)FindResource("TvButton"),
                    Foreground = isFav ? (Brush)FindResource("TextPrimary") : (Brush)FindResource("TextMuted"),
                    ToolTip = "Add to / remove from the toolbar (max 6)",
                };
                starBtn.Click += TimeframeStar_Click;

                Grid.SetColumn(selectBtn, 0);
                Grid.SetColumn(starBtn, 1);
                row.Children.Add(selectBtn);
                row.Children.Add(starBtn);
                groupPanel.Children.Add(row);
            }

            TimeframeMenuPanel.Children.Add(groupPanel);
        }
    }

    // Collapses / expands one section of the timeframe menu.
    private void TimeframeGroupHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string group) return;
        if (!_collapsedGroups.Remove(group)) _collapsedGroups.Add(group);
        BuildTimeframeMenu();
    }

    private async void TimeframeMenuSelect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tf) return;
        TimeframePopup.IsOpen = false;
        if (tf == _chartTimeframe) return;
        _chartTimeframe = tf;
        if (_activeTab is not null) _activeTab.Timeframe = tf;
        RebuildTimeframeBar();
        await LoadChartAsync();
    }

    private void TimeframeStar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tf) return;

        // Remove returns false when the item wasn't there → then we add it
        // (respecting the 6-star cap).
        if (!_favoriteTfs.Remove(tf))
        {
            if (_favoriteTfs.Count >= MaxFavoriteTimeframes)
            {
                NotificationService.ShowToast("Meowgnal", "Maximum 6 favorite timeframes — unstar one first.");
                return;
            }
            _favoriteTfs.Add(tf);
        }

        var settings = SettingsStorageService.Load();
        settings.FavoriteTimeframes = _favoriteTfs;
        SettingsStorageService.Save(settings);

        RebuildTimeframeBar();
        BuildTimeframeMenu();
    }

    // ------------------------------------------------------------------
    // Chart type dropdown (TradingView style, with mini icons)
    // ------------------------------------------------------------------

    private void ChartTypeButton_Click(object sender, RoutedEventArgs e) =>
        ChartTypePopup.IsOpen = !ChartTypePopup.IsOpen;

    private void ChartTypeItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string type) return;
        ChartTypePopup.IsOpen = false;
        ApplyChartType(type);
        _ = SendChartTypeAsync(type);
    }

    // Updates the toolbar button's icon + label for the given chart type.
    private void ApplyChartType(string type)
    {
        _chartType = type;
        if (_activeTab is not null) _activeTab.ChartType = type;
        ChartTypeIcon.Content = FindResource("Icon_" + type);
        ChartTypeLabel.Text = type switch
        {
            "line" => "Line",
            "area" => "Area",
            "heikinashi" => "Heikin Ashi",
            "bars" => "Bars (OHLC)",
            _ => "Candles"
        };
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
        try
        {
            IDataProvider provider = _chartDataSource == "hyperliquid" ? new HyperliquidDataProvider() : new BinanceDataProvider();
            var bars = await provider.GetHistoricalCandlesAsync(_chartSymbol, _chartTimeframe, limit: 1000);
            await UpdateChartAsync(bars);
            SymbolText.Text = _chartSymbol;
            PriceText.Text = bars[^1].Close.ToString("N2");
        }
        catch (Exception ex)
        {
            // e.g. a timeframe the current exchange doesn't support (seconds
            // on Hyperliquid), or a temporary network failure — never crash.
            MessageBox.Show(
                $"Could not load chart data for {_chartSymbol} / {_chartTimeframe}:\n{ex.Message}",
                "Meowgnal",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task LoadDashboardAsync()
    {
        var strategies = StrategyStorageService.LoadAll();
        _signals.Clear();
        ActiveStrategiesText.Text = strategies.Count.ToString();

        if (strategies.Count == 0)
        {
            WinRateText.Text = "—";
            SignalCountText.Text = "0";
            return;
        }

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
            // Second-based timeframes need seconds shown on the time axis.
            secondsVisible = _chartTimeframe.EndsWith('s'),
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