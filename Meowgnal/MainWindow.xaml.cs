using Meowgnal.DataProviders;
using Meowgnal.Engine;
using Meowgnal.Models;
using Meowgnal.Services;
using Meowgnal.Views;
using Drawing = Meowgnal.Models.Drawing;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Meowgnal;

public partial class MainWindow : Window
{
    private sealed class ChartTab
    {
        public string Symbol { get; set; } = "BTC/USDT";
        public string Timeframe { get; set; } = "1h";
        public string ChartType { get; set; } = "candles";
        public string DataSource { get; set; } = "binance";
    }

    private sealed class WatchlistRow
    {
        public WatchlistItem Item { get; init; } = new();
        public TextBlock LastText { get; init; } = new();
        public TextBlock ChgText { get; init; } = new();
    }

    private sealed class PaperPositionRow
    {
        public PaperPosition Position { get; init; } = new();
        public TextBlock PriceText { get; init; } = new();
        public TextBlock PnLText { get; init; } = new();
    }

    private readonly List<ChartTab> _tabs = new();
    private ChartTab? _activeTab;

    private WatchlistsFile _watchlistsFile = new();
    private WatchlistDefinition _activeWatchlist = new();
    private readonly List<WatchlistRow> _watchlistRows = new();
    private readonly DispatcherTimer _watchTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _refreshingWatchlist;

    private PaperAccountFile _paperAccount = new();
    private readonly List<PaperPositionRow> _paperRows = new();

    private readonly DispatcherTimer _symbolPreviewDebounce = new() { Interval = TimeSpan.FromMilliseconds(600) };

    private readonly ObservableCollection<SignalDisplayItem> _signals = new();

    private readonly TaskCompletionSource<bool> _chartPageReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private DispatcherTimer? _monitorTimer;
    private readonly HashSet<string> _knownSignalKeys = new();
    private bool _baselineSeeded;
    private bool _isScanning;

    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    // Drawing tools state
    private DrawingsFile _drawingsFile = new();
    private string? _activeDrawingMode = null;

    // Price alerts state
    private PriceAlertsFile _alerts = new();

    private static readonly SolidColorBrush UpBrush = new(Color.FromRgb(0x08, 0x99, 0x81));
    private static readonly SolidColorBrush DownBrush = new(Color.FromRgb(0xF2, 0x36, 0x45));

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
        // Apply saved theme before UI is built
        ThemeService.ApplyTheme(SettingsStorageService.Load());

        // 1. Show Splash Screen
        var splash = new SplashWindow();
        splash.Show();

        // Keep UI responsive while splash is visible
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 1200)
        {
            Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
        }

        InitializeComponent();

        // Set version in custom title bar
        var v = Assembly.GetEntryAssembly()?.GetName().Version;
        TitleBarVersion.Text = v is null ? "" : $"v{v.Major}.{v.Minor}";

        splash.Close();

        SignalsList.ItemsSource = _signals;

        ChartWebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0x13, 0x17, 0x22);

        ApplyChartType("candles");
        SetActiveTool(null);

        _favoriteTfs = SettingsStorageService.Load().FavoriteTimeframes;
        RebuildTimeframeBar();

        ApplyClockSettings();
        UpdateClockText();
        _clockTimer.Tick += (_, _) => UpdateClockText();
        _clockTimer.Start();

        _watchTimer.Tick += WatchTimer_Tick;
        _watchTimer.Start();

        _symbolPreviewDebounce.Tick += async (_, _) =>
        {
            _symbolPreviewDebounce.Stop();
            await UpdateSymbolPreviewAsync();
        };

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = SettingsStorageService.Load();

        // 2. First Run Onboarding
        if (!settings.FirstRunCompleted)
        {
            var onboard = new OnboardingWindow { Owner = this };
            if (onboard.ShowDialog() == true)
            {
                settings.FirstRunCompleted = true;
                if (!onboard.ChoseGuest)
                {
                    settings.ProfileName = onboard.ChosenName;
                    settings.ProfileAvatar = onboard.ChosenAvatar;
                    settings.IsGuest = false;
                }
                else
                {
                    settings.IsGuest = true;
                    settings.ProfileName = "Guest";
                    settings.ProfileAvatar = "🐱";
                    LicenseService.EnsureDemoStarted(settings);
                }
                SettingsStorageService.Save(settings);
            }
            else
            {
                Application.Current.Shutdown();
                return;
            }
        }
        else if (settings.IsGuest)
        {
            LicenseService.EnsureDemoStarted(settings);
        }

        // 3. License Check
        var access = LicenseService.CheckAccess(settings);
        if (!access.Allowed)
        {
            MessageBox.Show(access.Message, "Meowgnal — Demo Expired", MessageBoxButton.OK, MessageBoxImage.Warning);
            Application.Current.Shutdown();
            return;
        }

        UpdateProfileMenu(settings, access.Message);

        _ = InitializeChartWebViewAsync();

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

        _drawingsFile = DrawingStorageService.Load();
        _alerts = PriceAlertStorageService.Load();
        _watchlistsFile = WatchlistStorageService.Load();
        _activeWatchlist = _watchlistsFile.Lists.FirstOrDefault(l => l.Name == _watchlistsFile.ActiveListName)
                           ?? _watchlistsFile.Lists[0];
        WatchlistNameText.Text = _activeWatchlist.Name;
        RebuildWatchlistPanel();
        _ = RefreshWatchlistPricesAsync();

        _paperAccount = PaperAccountStorageService.Load();
        PaperTradingEngine.CheckDailyReset(_paperAccount);
        RebuildPaperPanel();
        AutoTradeCheck.IsChecked = SettingsStorageService.Load().PaperAutoTradeEnabled;

        await LoadDashboardAsync();
        StartSignalMonitor();
    }

    private void UpdateProfileMenu(AppSettings settings, string statusMsg)
    {
        ProfileAvatarText.Text = settings.ProfileAvatar;
        MenuAvatarText.Text = settings.ProfileAvatar;
        MenuNameText.Text = settings.ProfileName;
        MenuStatusText.Text = statusMsg;
    }

    #region Title Bar Controls
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else
        {
            if (WindowState == WindowState.Maximized)
            {
                var point = PointToScreen(e.GetPosition(this));
                WindowState = WindowState.Normal;
                Left = point.X - Width / 2;
                Top = point.Y - 15;
            }
            DragMove();
        }
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

    #region Profile Menu
    private void ProfileButton_Click(object sender, RoutedEventArgs e) =>
        ProfilePopup.IsOpen = !ProfilePopup.IsOpen;

    private void MenuSettings_Click(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        OpenSettingsButton_Click(sender, e);
    }

    private void MenuLicense_Click(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        MessageBox.Show("Dedicated License Activation window will be added in the next step.\nFor now, the demo period is automatically tracked.", "Meowgnal", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MenuHelp_Click(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/meowrypto/Meowgnal") { UseShellExecute = true });
        }
        catch { }
    }

    private void MenuWhatsNew_Click(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        MessageBox.Show("Welcome to Meowgnal!\n\n- Custom Title Bar\n- Profile & Onboarding\n- Splash Screen\n- License Management", "What's new", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MenuSignOut_Click(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        var res = MessageBox.Show("Sign out and switch profile? This will restart the app.", "Meowgnal", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            var settings = SettingsStorageService.Load();
            settings.FirstRunCompleted = false;
            settings.LicenseKey = "";
            SettingsStorageService.Save(settings);
            Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }
    }

    private void ApplyAndSaveTheme(string theme)
    {
        var settings = SettingsStorageService.Load();
        settings.Theme = theme;
        SettingsStorageService.Save(settings);
        ThemeService.ApplyTheme(settings);
        _ = SendThemeToChartAsync();
    }

    private void ThemeDark_Click(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        ApplyAndSaveTheme("dark");
    }

    private void ThemeLight_Click(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        ApplyAndSaveTheme("light");
    }

    private void ThemeSystem_Click(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        ApplyAndSaveTheme("system");
    }

    private void ThemeCustom_Click(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        var win = new ThemeCustomizerWindow { Owner = this };
        if (win.ShowDialog() == true)
            ApplyAndSaveTheme("custom");
    }

    private async Task SendThemeToChartAsync()
    {
        try { await _chartPageReady.Task; } catch { return; }
        if (ChartWebView.CoreWebView2 is null) return;

        ChartWebView.CoreWebView2.PostWebMessageAsJson(
            JsonSerializer.Serialize(new { type = "setTheme", colors = ThemeService.GetChartColors(SettingsStorageService.Load()) }));
    }
    #endregion

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
        if (_tabs.Count <= 1) return;

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

        var existing = _tabs.FirstOrDefault(t => t.Symbol == symbol);
        if (existing is not null)
        {
            await ActivateTabAsync(existing);
            return;
        }

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

    private static string? NormalizeSymbol(string input)
    {
        var s = input.Trim().ToUpperInvariant();
        if (s.Length == 0) return null;
        if (s.Contains('/')) return s;
        if (s.EndsWith("USDT") && s.Length > 4) return s[..^4] + "/USDT";
        if (s.EndsWith("USD") && s.Length > 3) return s[..^3] + "/USD";
        return s + "/USDT";
    }

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

    private void SetRightTab(string which)
    {
        WatchlistPane.Visibility = which == "watchlist" ? Visibility.Visible : Visibility.Collapsed;
        SignalsPane.Visibility = which == "signals" ? Visibility.Visible : Visibility.Collapsed;
        PaperPane.Visibility = which == "paper" ? Visibility.Visible : Visibility.Collapsed;

        TabWatchlistButton.Background = which == "watchlist" ? (Brush)FindResource("Accent") : Brushes.Transparent;
        TabWatchlistButton.Foreground = which == "watchlist" ? Brushes.White : (Brush)FindResource("TextSecondary");
        TabSignalsButton.Background = which == "signals" ? (Brush)FindResource("Accent") : Brushes.Transparent;
        TabSignalsButton.Foreground = which == "signals" ? Brushes.White : (Brush)FindResource("TextSecondary");
        TabPaperButton.Background = which == "paper" ? (Brush)FindResource("Accent") : Brushes.Transparent;
        TabPaperButton.Foreground = which == "paper" ? Brushes.White : (Brush)FindResource("TextSecondary");
    }

    private void RightTabWatchlist_Click(object sender, RoutedEventArgs e) => SetRightTab("watchlist");
    private void RightTabSignals_Click(object sender, RoutedEventArgs e) => SetRightTab("signals");
    private void RightTabPaper_Click(object sender, RoutedEventArgs e) => SetRightTab("paper");

    private void RebuildWatchlistPanel()
    {
        WatchlistRowsPanel.Children.Clear();
        _watchlistRows.Clear();

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = "Symbol", Foreground = (Brush)FindResource("TextMuted"), FontSize = 10 });
        var hLast = new TextBlock { Text = "Last", Foreground = (Brush)FindResource("TextMuted"), FontSize = 10, Margin = new Thickness(0, 0, 14, 0) };
        Grid.SetColumn(hLast, 1);
        var hChg = new TextBlock { Text = "Chg%", Foreground = (Brush)FindResource("TextMuted"), FontSize = 10 };
        Grid.SetColumn(hChg, 2);
        header.Children.Add(hLast);
        header.Children.Add(hChg);
        WatchlistRowsPanel.Children.Add(header);

        foreach (var item in _activeWatchlist.Items)
        {
            var row = new Grid { Margin = new Thickness(0, 7, 0, 0), Cursor = Cursors.Hand, Tag = item };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var sym = new TextBlock
            {
                Text = item.Symbol,
                Foreground = (Brush)FindResource("TextPrimary"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var last = new TextBlock
            {
                Text = "—",
                Foreground = (Brush)FindResource("TextSecondary"),
                FontSize = 12,
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(last, 1);
            var chg = new TextBlock
            {
                Text = "—",
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 12,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(chg, 2);
            var del = new TextBlock
            {
                Text = "✕",
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 10,
                Tag = item,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(del, 3);
            del.MouseLeftButtonUp += RemoveSymbol_Click;

            row.Children.Add(sym);
            row.Children.Add(last);
            row.Children.Add(chg);
            row.Children.Add(del);
            row.MouseLeftButtonUp += WatchlistRow_Click;

            WatchlistRowsPanel.Children.Add(row);
            _watchlistRows.Add(new WatchlistRow { Item = item, LastText = last, ChgText = chg });
        }
    }

    private async void WatchlistRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Grid g || g.Tag is not WatchlistItem item) return;

        var existing = _tabs.FirstOrDefault(t => t.Symbol == item.Symbol);
        if (existing is not null)
        {
            await ActivateTabAsync(existing);
            return;
        }

        var tab = new ChartTab { Symbol = item.Symbol, DataSource = item.DataSource };
        _tabs.Add(tab);
        await ActivateTabAsync(tab);
    }

    private void RemoveSymbol_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is not TextBlock t || t.Tag is not WatchlistItem item) return;

        _activeWatchlist.Items.Remove(item);
        SaveWatchlists();
        RebuildWatchlistPanel();
        _ = RefreshWatchlistPricesAsync();
    }

    private void WatchlistNameButton_Click(object sender, RoutedEventArgs e)
    {
        if (WatchlistSwitchPopup.IsOpen)
        {
            WatchlistSwitchPopup.IsOpen = false;
            return;
        }

        SwitchListPanel.Children.Clear();
        foreach (var list in _watchlistsFile.Lists)
        {
            var btn = new Button
            {
                Style = (Style)FindResource("TvButtonLeft"),
                Tag = list.Name,
                Content = list == _activeWatchlist ? list.Name + "  ★" : list.Name,
            };
            btn.Click += SwitchList_Click;
            SwitchListPanel.Children.Add(btn);
        }
        WatchlistSwitchPopup.IsOpen = true;
    }

    private void SwitchList_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string name) return;
        WatchlistSwitchPopup.IsOpen = false;

        var list = _watchlistsFile.Lists.FirstOrDefault(l => l.Name == name);
        if (list is null || list == _activeWatchlist) return;

        _activeWatchlist = list;
        _watchlistsFile.ActiveListName = list.Name;
        SaveWatchlists();
        WatchlistNameText.Text = list.Name;
        RebuildWatchlistPanel();
        _ = RefreshWatchlistPricesAsync();
    }

    private void NewWatchlistButton_Click(object sender, RoutedEventArgs e)
    {
        NewWatchlistNameBox.Text = "";
        NewWatchlistPopup.IsOpen = !NewWatchlistPopup.IsOpen;
    }

    private void NewWatchlistConfirm_Click(object sender, RoutedEventArgs e)
    {
        var name = NewWatchlistNameBox.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show("Please enter a name for the watchlist.", "Meowgnal",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (_watchlistsFile.Lists.Any(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            NotificationService.ShowToast("Meowgnal", "A watchlist with this name already exists.");
            return;
        }

        var list = new WatchlistDefinition { Name = name };
        _watchlistsFile.Lists.Add(list);
        _watchlistsFile.ActiveListName = name;
        _activeWatchlist = list;
        SaveWatchlists();

        NewWatchlistPopup.IsOpen = false;
        WatchlistNameText.Text = name;
        RebuildWatchlistPanel();
    }

    private void AddSymbolButton_Click(object sender, RoutedEventArgs e)
    {
        if (AddSymbolPopup.IsOpen)
        {
            AddSymbolPopup.IsOpen = false;
            return;
        }
        AddSymbolPopup.IsOpen = true;
        _ = UpdateSymbolPreviewAsync();
    }

    private void AddSymbolBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _symbolPreviewDebounce.Stop();
        _symbolPreviewDebounce.Start();
    }

    private async Task UpdateSymbolPreviewAsync()
    {
        var symbol = NormalizeSymbol(AddSymbolBox.Text);
        if (symbol is null)
        {
            SourceBinanceRadio.Content = "Binance — …";
            SourceHyperRadio.Content = "Hyperliquid — …";
            return;
        }

        var binanceTask = SafeTickerAsync(new BinanceDataProvider(), symbol);
        var hyperTask = SafeTickerAsync(new HyperliquidDataProvider(), symbol);
        await Task.WhenAll(binanceTask, hyperTask);

        var b = binanceTask.Result;
        var h = hyperTask.Result;

        SourceBinanceRadio.IsEnabled = b is not null;
        SourceBinanceRadio.Content = b is null ? "Binance — not available" : $"Binance — {FormatPrice(b.Last)}";
        SourceHyperRadio.IsEnabled = h is not null;
        SourceHyperRadio.Content = h is null ? "Hyperliquid — not available" : $"Hyperliquid — {FormatPrice(h.Last)}";

        if (b is not null) SourceBinanceRadio.IsChecked = true;
        else if (h is not null) SourceHyperRadio.IsChecked = true;
    }

    private static async Task<TickerInfo?> SafeTickerAsync(IDataProvider provider, string symbol)
    {
        try
        {
            var map = await provider.GetTickersAsync(new[] { symbol });
            return map.TryGetValue(symbol, out var t) ? t : null;
        }
        catch
        {
            return null;
        }
    }

    private void AddSymbolConfirm_Click(object sender, RoutedEventArgs e)
    {
        var symbol = NormalizeSymbol(AddSymbolBox.Text);
        if (symbol is null)
        {
            MessageBox.Show("Please enter a valid symbol, e.g. BTC/USDT", "Meowgnal",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var source = SourceHyperRadio.IsChecked == true ? "hyperliquid" : "binance";

        if (_activeWatchlist.Items.Any(i => i.Symbol == symbol && i.DataSource == source))
        {
            NotificationService.ShowToast("Meowgnal", $"{symbol} is already in this list.");
            return;
        }

        _activeWatchlist.Items.Add(new WatchlistItem { Symbol = symbol, DataSource = source });
        SaveWatchlists();
        AddSymbolPopup.IsOpen = false;
        RebuildWatchlistPanel();
        _ = RefreshWatchlistPricesAsync();
    }

    private void SaveWatchlists() => WatchlistStorageService.Save(_watchlistsFile);

    private async void WatchTimer_Tick(object? sender, EventArgs e)
    {
        if (_refreshingWatchlist) return;
        _refreshingWatchlist = true;
        try
        {
            await RefreshWatchlistPricesAsync();
            await UpdatePaperLiveAsync();
            await CheckPriceAlertsAsync();
        }
        finally
        {
            _refreshingWatchlist = false;
        }
    }

    private async Task RefreshWatchlistPricesAsync()
    {
        if (_watchlistRows.Count == 0) return;

        foreach (var group in _watchlistRows.GroupBy(r => r.Item.DataSource).ToList())
        {
            try
            {
                IDataProvider provider = group.Key == "hyperliquid"
                    ? new HyperliquidDataProvider()
                    : new BinanceDataProvider();
                var tickers = await provider.GetTickersAsync(group.Select(r => r.Item.Symbol).Distinct());

                foreach (var row in group)
                {
                    if (!tickers.TryGetValue(row.Item.Symbol, out var t)) continue;
                    row.LastText.Text = FormatPrice(t.Last);
                    row.ChgText.Text = $"{t.ChgPercent:+0.00;-0.00;0.00}%";
                    row.ChgText.Foreground = t.ChgPercent >= 0 ? UpBrush : DownBrush;
                }
            }
            catch
            {
            }
        }
    }

    private static string FormatPrice(decimal price) =>
        price >= 1000 ? price.ToString("N2") :
        price >= 1 ? price.ToString("N4") :
        price.ToString("0.00000000");

    private void SavePaperAccount() => PaperAccountStorageService.Save(_paperAccount);

    private void AutoTradeCheck_Click(object sender, RoutedEventArgs e)
    {
        var settings = SettingsStorageService.Load();
        settings.PaperAutoTradeEnabled = AutoTradeCheck.IsChecked == true;
        SettingsStorageService.Save(settings);
    }

    private void RebuildPaperPanel()
    {
        PaperPositionsPanel.Children.Clear();
        PaperHistoryPanel.Children.Clear();
        _paperRows.Clear();

        if (_paperAccount.OpenPositions.Count == 0)
        {
            PaperPositionsPanel.Children.Add(new TextBlock
            {
                Text = _paperAccount.IsSuspendedUntilTomorrow
                    ? "Suspended until tomorrow (UTC)."
                    : "No open positions.",
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 11,
            });
        }

        foreach (var pos in _paperAccount.OpenPositions)
        {
            var border = new Border
            {
                Background = (Brush)FindResource("BgPanel"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 6),
            };
            var sp = new StackPanel();

            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var symBox = new StackPanel { Orientation = Orientation.Horizontal };
            symBox.Children.Add(new TextBlock
            {
                Text = pos.Symbol,
                Foreground = (Brush)FindResource("TextPrimary"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
            });
            var sideBadge = new Border
            {
                Background = pos.Side == PositionSide.Long ? UpBrush : DownBrush,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(6, 1, 0, 0),
            };
            sideBadge.Child = new TextBlock
            {
                Text = pos.Side == PositionSide.Long ? "LONG" : "SHORT",
                Foreground = Brushes.White,
                FontSize = 9,
            };
            symBox.Children.Add(sideBadge);
            symBox.Children.Add(new TextBlock
            {
                Text = $"  {pos.Leverage:0}x",
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
            });
            topRow.Children.Add(symBox);

            var marginText = new TextBlock
            {
                Text = $"Margin {pos.Margin:N2}",
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(marginText, 1);
            topRow.Children.Add(marginText);
            sp.Children.Add(topRow);

            sp.Children.Add(new TextBlock
            {
                Text = $"{pos.Size} @ {FormatPrice(pos.EntryPrice)}",
                Foreground = (Brush)FindResource("TextSecondary"),
                FontSize = 10,
                Margin = new Thickness(0, 3, 0, 0),
            });

            var bottomRow = new Grid();
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var priceText = new TextBlock
            {
                Text = "—",
                Foreground = (Brush)FindResource("TextSecondary"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
            };
            bottomRow.Children.Add(priceText);
            var pnlText = new TextBlock
            {
                Text = "—",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
            };
            Grid.SetColumn(pnlText, 1);
            bottomRow.Children.Add(pnlText);
            sp.Children.Add(bottomRow);

            sp.Children.Add(new TextBlock
            {
                Text = $"SL {(pos.StopLoss > 0 ? FormatPrice(PaperTradingEngine.EffectiveStopLoss(pos)) : "—")}  ·  " +
                       $"TP {(pos.TakeProfit > 0 ? FormatPrice(pos.TakeProfit) : "—")}  ·  " +
                       $"Liq {FormatPrice(pos.LiquidationPrice)}",
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 9,
                Margin = new Thickness(0, 3, 0, 0),
            });

            var closeBtn = new Button
            {
                Content = "⏹ Close position",
                Style = (Style)FindResource("TvButton"),
                Foreground = DownBrush,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 6, 0, 0),
                Tag = pos,
                ToolTip = "Close at market price",
            };
            closeBtn.Click += PaperClose_Click;
            sp.Children.Add(closeBtn);

            border.Child = sp;
            PaperPositionsPanel.Children.Add(border);
            _paperRows.Add(new PaperPositionRow { Position = pos, PriceText = priceText, PnLText = pnlText });
        }

        if (_paperAccount.TradeHistory.Count == 0)
        {
            PaperHistoryPanel.Children.Add(new TextBlock
            {
                Text = "No closed trades yet.",
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 11,
            });
        }

        foreach (var trade in _paperAccount.TradeHistory.Take(10))
        {
            var tsp = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

            var line1 = new Grid();
            line1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { Orientation = Orientation.Horizontal };
            left.Children.Add(new TextBlock
            {
                Text = trade.Symbol,
                Foreground = (Brush)FindResource("TextPrimary"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
            });
            left.Children.Add(new TextBlock
            {
                Text = trade.Side == PositionSide.Long ? "  LONG" : "  SHORT",
                Foreground = trade.Side == PositionSide.Long ? UpBrush : DownBrush,
                FontSize = 9,
            });
            line1.Children.Add(left);

            var pnl = new TextBlock
            {
                Text = $"{trade.PnL:+0.00;-0.00}",
                Foreground = trade.PnL >= 0 ? UpBrush : DownBrush,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
            };
            Grid.SetColumn(pnl, 1);
            line1.Children.Add(pnl);
            tsp.Children.Add(line1);

            tsp.Children.Add(new TextBlock
            {
                Text = $"{trade.Reason} · {trade.CloseTime:MM/dd HH:mm}",
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 9,
                Margin = new Thickness(0, 1, 0, 0),
            });

            PaperHistoryPanel.Children.Add(tsp);
        }

        UpdatePaperSummary(new Dictionary<string, decimal>());
    }

    private void UpdatePaperSummary(Dictionary<string, decimal> prices)
    {
        var settings = SettingsStorageService.Load();

        decimal unrealized = 0m;
        foreach (var p in _paperAccount.OpenPositions)
            if (prices.TryGetValue(p.Symbol, out var px))
                unrealized += p.UnrealizedPnL(px, settings.PaperTakerFeePercent);

        var equity = PaperTradingEngine.Equity(
            _paperAccount,
            p => prices.TryGetValue(p.Symbol, out var px) ? px : p.EntryPrice,
            settings.PaperTakerFeePercent);

        PaperBalanceText.Text = $"{_paperAccount.CurrentBalance:N2} USDT";
        PaperUnrealizedText.Text = $"{unrealized:+0.00;-0.00} USDT";
        PaperUnrealizedText.Foreground = unrealized >= 0 ? UpBrush : DownBrush;
        PaperEquityText.Text = $"Equity: {equity:N2} USDT";

        if (_paperAccount.IsSuspendedUntilTomorrow)
        {
            PaperStatusText.Text = "💼 Paper: suspended (daily loss limit)";
            PaperStatusText.Foreground = DownBrush;
        }
        else if (_paperAccount.OpenPositions.Count == 0)
        {
            PaperStatusText.Text = $"💼 Paper: {equity:N2} USDT";
            PaperStatusText.Foreground = (Brush)FindResource("TextMuted");
        }
        else
        {
            PaperStatusText.Text = $"💼 Paper: {equity:N2} USDT ({unrealized:+0.00;-0.00})";
            PaperStatusText.Foreground = unrealized >= 0 ? UpBrush : DownBrush;
        }
    }

    private void OpenPositionButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = SettingsStorageService.Load();
        PopSymbolBox.Text = _chartSymbol;
        PopLeverageBox.Text = settings.PaperDefaultLeverage.ToString();
        PopSLBox.Text = settings.PaperDefaultStopLossPercent.ToString();
        PopTPBox.Text = settings.PaperDefaultTakeProfitPercent.ToString();
        PopTrailingCheck.IsChecked = false;
        PopTrailingDistBox.Text = "2";
        PopTrailingActBox.Text = "2";
        PopSideLong.IsChecked = true;

        var balance = _paperAccount.CurrentBalance;
        var slDefault = settings.PaperDefaultStopLossPercent;
        var suggested = settings.PaperUseRiskBasedSizing && slDefault > 0
            ? balance * settings.PaperRiskPercentPerTrade / (slDefault * settings.PaperDefaultLeverage)
            : balance * settings.PaperPositionSizePercent / 100m;
        if (suggested <= 0) suggested = 100m;
        PopMarginBox.Text = Math.Round(Math.Min(suggested, balance), 2).ToString();
        OpenPositionPopup.IsOpen = true;
    }

    private async void OpenPositionConfirm_Click(object sender, RoutedEventArgs e)
    {
        var symbol = NormalizeSymbol(PopSymbolBox.Text);
        if (symbol is null)
        {
            NotificationService.ShowToast("Meowgnal", "Please enter a valid symbol, e.g. BTC/USDT.");
            return;
        }

        var settings = SettingsStorageService.Load();
        var side = PopSideShort.IsChecked == true ? PositionSide.Short : PositionSide.Long;
        var leverage = decimal.TryParse(PopLeverageBox.Text, out var lev) && lev >= 1 ? lev : settings.PaperDefaultLeverage;
        var slPct = decimal.TryParse(PopSLBox.Text, out var slp) && slp > 0 ? slp : 0m;
        var tpPct = decimal.TryParse(PopTPBox.Text, out var tpp) && tpp > 0 ? tpp : 0m;
        var trailing = PopTrailingCheck.IsChecked == true;
        var trailDist = decimal.TryParse(PopTrailingDistBox.Text, out var td) && td > 0 ? td : 2m;
        var trailAct = decimal.TryParse(PopTrailingActBox.Text, out var ta) && ta > 0 ? ta : 2m;
        var marginUsdt = decimal.TryParse(PopMarginBox.Text, out var mu) && mu > 0 ? mu : 0m;

        IDataProvider primary = _chartDataSource == "hyperliquid" ? new HyperliquidDataProvider() : new BinanceDataProvider();
        IDataProvider secondary = _chartDataSource == "hyperliquid" ? new BinanceDataProvider() : new HyperliquidDataProvider();
        var primaryName = _chartDataSource == "hyperliquid" ? "hyperliquid" : "binance";
        var secondaryName = _chartDataSource == "hyperliquid" ? "binance" : "hyperliquid";

        var ticker = await SafeTickerAsync(primary, symbol);
        var dataSource = primaryName;
        if (ticker is null)
        {
            ticker = await SafeTickerAsync(secondary, symbol);
            dataSource = secondaryName;
        }
        if (ticker is null)
        {
            NotificationService.ShowToast("Meowgnal", $"{symbol} is not available on either exchange.");
            return;
        }
        var entry = ticker.Last;

        var slPrice = slPct > 0
            ? (side == PositionSide.Long ? entry * (1m - slPct / 100m) : entry * (1m + slPct / 100m))
            : 0m;
        var tpPrice = tpPct > 0
            ? (side == PositionSide.Long ? entry * (1m + tpPct / 100m) : entry * (1m - tpPct / 100m))
            : 0m;

        var existingPos = _paperAccount.OpenPositions.FirstOrDefault(p => p.Symbol == symbol);
        if (existingPos is not null)
        {
            PaperTradingEngine.Close(_paperAccount, existingPos, entry, CloseReason.Manual, settings.PaperTakerFeePercent);
            CheckDailySuspension(settings);
        }

        var result = PaperTradingEngine.TryOpen(
            _paperAccount, settings, symbol, dataSource, side, entry, leverage,
            slPrice, tpPrice, trailing, trailDist, trailAct, marginUsdt, strategyId: null);

        if (!result.Ok)
        {
            NotificationService.ShowToast("Meowgnal", result.Error);
            return;
        }

        SavePaperAccount();
        OpenPositionPopup.IsOpen = false;
        RebuildPaperPanel();
        _ = SendPositionsToChartAsync();
        NotificationService.ShowToast("Meowgnal",
            $"{(side == PositionSide.Long ? "LONG" : "SHORT")} {symbol} opened: {result.Position!.Size} @ {FormatPrice(entry)} " +
            $"(margin {result.Position.Margin:N2} USDT, {leverage:0}x)");
    }

    private async void PaperClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not PaperPosition pos) return;

        var settings = SettingsStorageService.Load();
        IDataProvider provider = pos.DataSource == "hyperliquid"
            ? new HyperliquidDataProvider()
            : new BinanceDataProvider();
        var ticker = await SafeTickerAsync(provider, pos.Symbol);
        if (ticker is null)
        {
            NotificationService.ShowToast("Meowgnal", "Exchange unreachable — could not close the position.");
            return;
        }

        var trade = PaperTradingEngine.Close(_paperAccount, pos, ticker.Last, CloseReason.Manual, settings.PaperTakerFeePercent);
        CheckDailySuspension(settings);
        SavePaperAccount();
        RebuildPaperPanel();
        _ = SendPositionsToChartAsync();
        NotificationService.ShowToast("Meowgnal", $"{trade.Symbol} closed: PnL {trade.PnL:+0.00;-0.00} USDT");
    }

    private void CheckDailySuspension(AppSettings settings)
    {
        if (_paperAccount.IsSuspendedUntilTomorrow) return;
        if (PaperTradingEngine.DailyLossLimitBreached(_paperAccount, settings))
        {
            _paperAccount.IsSuspendedUntilTomorrow = true;
            NotificationService.ShowToast("Meowgnal — risk rule",
                "Max daily loss reached. Paper trading is suspended until tomorrow (UTC).");
        }
    }

    private async Task UpdatePaperLiveAsync()
    {
        var settings = SettingsStorageService.Load();
        PaperTradingEngine.CheckDailyReset(_paperAccount);

        if (_paperAccount.OpenPositions.Count == 0)
        {
            UpdatePaperSummary(new Dictionary<string, decimal>());
            return;
        }

        var prices = new Dictionary<string, decimal>();
        foreach (var group in _paperAccount.OpenPositions.GroupBy(p => p.DataSource).ToList())
        {
            try
            {
                IDataProvider provider = group.Key == "hyperliquid"
                    ? new HyperliquidDataProvider()
                    : new BinanceDataProvider();
                var tickers = await provider.GetTickersAsync(group.Select(p => p.Symbol).Distinct());
                foreach (var p in group)
                    if (tickers.TryGetValue(p.Symbol, out var t)) prices[p.Symbol] = t.Last;
            }
            catch
            {
            }
        }

        var highs = new Dictionary<string, decimal>();
        var lows = new Dictionary<string, decimal>();
        foreach (var symbol in _paperAccount.OpenPositions.Select(p => p.Symbol).Distinct())
        {
            try
            {
                var src = _paperAccount.OpenPositions.First(p => p.Symbol == symbol).DataSource;
                IDataProvider provider = src == "hyperliquid"
                    ? new HyperliquidDataProvider()
                    : new BinanceDataProvider();
                var candles = await provider.GetHistoricalCandlesAsync(symbol, "1m", limit: 1);
                if (candles.Count > 0)
                {
                    highs[symbol] = candles[0].High;
                    lows[symbol] = candles[0].Low;
                }
            }
            catch
            {
            }
        }

        var closedTrades = new List<PaperTrade>();

        foreach (var pos in _paperAccount.OpenPositions.ToList())
        {
            if (!prices.TryGetValue(pos.Symbol, out var last)) continue;

            PaperTradingEngine.UpdateTrailing(pos, last);

            var checkHigh = Math.Max(last, highs.TryGetValue(pos.Symbol, out var h) ? h : last);
            var checkLow = Math.Min(last, lows.TryGetValue(pos.Symbol, out var l) ? l : last);

            var reason = PaperTradingEngine.CheckStops(pos, checkHigh, checkLow);
            if (reason is not null)
                closedTrades.Add(PaperTradingEngine.Close(_paperAccount, pos, last, reason.Value, settings.PaperTakerFeePercent));
        }

        if (!_paperAccount.IsSuspendedUntilTomorrow &&
            PaperTradingEngine.DailyLossLimitBreached(_paperAccount, settings))
        {
            foreach (var pos in _paperAccount.OpenPositions.ToList())
            {
                if (prices.TryGetValue(pos.Symbol, out var px))
                    closedTrades.Add(PaperTradingEngine.Close(_paperAccount, pos, px, CloseReason.RiskRule, settings.PaperTakerFeePercent));
            }
            _paperAccount.IsSuspendedUntilTomorrow = true;
            NotificationService.ShowToast("Meowgnal — risk rule",
                "Max daily loss reached. All positions closed; paper trading suspended until tomorrow (UTC).");
        }

        foreach (var trade in closedTrades)
            NotificationService.ShowToast($"Meowgnal — {trade.Symbol}",
                $"Position closed ({trade.Reason}): PnL {trade.PnL:+0.00;-0.00} USDT");

        if (closedTrades.Count > 0)
        {
            SavePaperAccount();
            RebuildPaperPanel();
            _ = SendPositionsToChartAsync();
        }

        foreach (var row in _paperRows)
        {
            if (!prices.TryGetValue(row.Position.Symbol, out var last)) continue;
            row.PriceText.Text = FormatPrice(last);
            var pnl = row.Position.UnrealizedPnL(last, settings.PaperTakerFeePercent);
            var roi = row.Position.UnrealizedRoiPercent(last, settings.PaperTakerFeePercent);
            row.PnLText.Text = $"{pnl:+0.00;-0.00} ({roi:+0.0;-0.0}%)";
            row.PnLText.Foreground = pnl >= 0 ? UpBrush : DownBrush;
        }

        UpdatePaperSummary(prices);
    }

    private async Task CheckPriceAlertsAsync()
    {
        if (_alerts.Alerts.Count == 0) return;

        foreach (var group in _alerts.Alerts.GroupBy(a => a.DataSource).ToList())
        {
            try
            {
                IDataProvider provider = group.Key == "hyperliquid"
                    ? new HyperliquidDataProvider()
                    : new BinanceDataProvider();
                var tickers = await provider.GetTickersAsync(group.Select(a => a.Symbol).Distinct());

                foreach (var alert in group.ToList())
                {
                    if (!tickers.TryGetValue(alert.Symbol, out var t)) continue;
                    var above = t.Last >= alert.Price;

                    if (alert.WasAbove is null)
                    {
                        alert.WasAbove = above;
                        continue;
                    }

                    if (above != alert.WasAbove)
                    {
                        _alerts.Alerts.Remove(alert);
                        NotificationService.ShowToast($"Meowgnal — {alert.Symbol}",
                            $"🔔 Price crossed {alert.Price:N2} (now {t.Last:N2})");
                        if (SettingsStorageService.Load().SoundNotificationsEnabled)
                            NotificationService.PlayAlertSound();
                    }
                }
            }
            catch
            {
            }
        }

        PriceAlertStorageService.Save(_alerts);
    }

    private async Task AutoTradeSignalsAsync(List<FoundSignal> fresh, AppSettings settings)
    {
        var changed = false;

        foreach (var f in fresh)
        {
            if (_paperAccount.IsSuspendedUntilTomorrow) break;

            var symbol = f.Strategy.Symbol;
            IDataProvider provider = f.Strategy.DataSource == "hyperliquid"
                ? new HyperliquidDataProvider()
                : new BinanceDataProvider();
            var ticker = await SafeTickerAsync(provider, symbol);
            if (ticker is null) continue;
            var price = ticker.Last;

            if (f.Signal.Type == SignalType.Entry)
            {
                if (_paperAccount.OpenPositions.Any(p => p.Symbol == symbol)) continue;

                var slPrice = settings.PaperDefaultStopLossPercent > 0
                    ? price * (1m - settings.PaperDefaultStopLossPercent / 100m)
                    : 0m;
                var tpPrice = settings.PaperDefaultTakeProfitPercent > 0
                    ? price * (1m + settings.PaperDefaultTakeProfitPercent / 100m)
                    : 0m;

                var result = PaperTradingEngine.TryOpen(
                    _paperAccount, settings, symbol, f.Strategy.DataSource, PositionSide.Long, price,
                    settings.PaperDefaultLeverage, slPrice, tpPrice,
                    trailingEnabled: false, trailingDistancePercent: 0m, trailingActivationPercent: 0m,
                    customMarginUsdt: 0m, strategyId: f.Strategy.StrategyId);

                if (result.Ok)
                {
                    changed = true;
                    NotificationService.ShowToast("Meowgnal — auto trade",
                        $"AUTO LONG {symbol} @ {FormatPrice(price)} (margin {result.Position!.Margin:N2} USDT) by {f.Strategy.Name}");
                }
            }
            else
            {
                var pos = _paperAccount.OpenPositions.FirstOrDefault(p => p.Symbol == symbol);
                if (pos is null) continue;

                var trade = PaperTradingEngine.Close(_paperAccount, pos, price, CloseReason.SignalExit, settings.PaperTakerFeePercent);
                changed = true;
                CheckDailySuspension(settings);
                NotificationService.ShowToast("Meowgnal — auto trade",
                    $"AUTO CLOSE {trade.Symbol}: PnL {trade.PnL:+0.00;-0.00} USDT ({f.Strategy.Name})");
            }
        }

        if (changed)
        {
            SavePaperAccount();
            RebuildPaperPanel();
            _ = SendPositionsToChartAsync();
        }
    }

    private async Task SendPositionsToChartAsync()
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

        var positions = _paperAccount.OpenPositions
            .Where(p => p.Symbol == _chartSymbol)
            .Select(p => new
            {
                side = p.Side == PositionSide.Long ? "long" : "short",
                leverage = p.Leverage,
                entryPrice = p.EntryPrice,
                stopLoss = PaperTradingEngine.EffectiveStopLoss(p),
                takeProfit = p.TakeProfit,
                liquidation = p.LiquidationPrice,
                openTime = new DateTimeOffset(p.OpenTime).ToUnixTimeSeconds(),
            })
            .ToArray();

        ChartWebView.CoreWebView2.PostWebMessageAsJson(
            JsonSerializer.Serialize(new { type = "setPositions", positions }));
    }

    private async Task InitializeChartWebViewAsync()
    {
        try
        {
            await ChartWebView.EnsureCoreWebView2Async();

            var core = ChartWebView.CoreWebView2;

            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;

            core.NavigationCompleted += (_, _) => _chartPageReady.TrySetResult(true);

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

    private void OnChartWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var json = e.WebMessageAsJson;
        using (var probe = JsonDocument.Parse(json))
        {
            if (probe.RootElement.ValueKind == JsonValueKind.String)
                json = probe.RootElement.GetString()!;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeProp)) return;
        var msgType = typeProp.GetString();

        if (msgType == "deleteDrawing")
        {
            var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            if (!string.IsNullOrEmpty(id))
            {
                _drawingsFile.Drawings.RemoveAll(d => d.Id == id);
                DrawingStorageService.Save(_drawingsFile);
                _ = SendDrawingsToChartAsync();
            }
            return;
        }

        if (msgType == "copyPrice")
        {
            var price = root.GetProperty("price").GetDecimal();
            try { Clipboard.SetText(price.ToString()); } catch { }
            NotificationService.ShowToast("Meowgnal", $"Price {price:N2} copied to clipboard.");
            return;
        }

        if (msgType == "addAlert")
        {
            var price = root.GetProperty("price").GetDecimal();
            _alerts.Alerts.Add(new PriceAlert { Symbol = _chartSymbol, DataSource = _chartDataSource, Price = price });
            PriceAlertStorageService.Save(_alerts);
            NotificationService.ShowToast("Meowgnal", $"🔔 Alert added: {_chartSymbol} @ {price:N2}");
            return;
        }

        if (msgType == "openChartSettings")
        {
            var win = new ChartSettingsWindow { Owner = this };
            if (win.ShowDialog() == true)
                _ = SendThemeToChartAsync();
            _alerts = PriceAlertStorageService.Load();
            return;
        }

        if (msgType == "requestDrawings")
        {
            _ = SendDrawingsToChartAsync();
            return;
        }

        if (msgType == "drawingCompleted")
        {
            try
            {
                if (root.TryGetProperty("drawing", out var drawingEl))
                {
                    var kindStr = drawingEl.TryGetProperty("kind", out var k) ? k.GetString() : "horizontal";
                    var normalizedKind = kindStr == "fib" ? "fibonacci" : kindStr;
                    var kind = Enum.TryParse<DrawingKind>(normalizedKind, true, out var parsedKind)
                        ? parsedKind
                        : DrawingKind.HorizontalLine;

                    var newDrawing = new Drawing { Kind = kind, Symbol = _chartSymbol.Replace("/", "") };

                    if (drawingEl.TryGetProperty("points", out var pts))
                    {
                        foreach (var pt in pts.EnumerateArray())
                        {
                            newDrawing.Points.Add(new DrawingPoint
                            {
                                TimeUnix = pt.GetProperty("time").GetInt64(),
                                Price = pt.GetProperty("price").GetDecimal(),
                            });
                        }
                    }

                    if (newDrawing.Points.Count > 0)
                    {
                        _drawingsFile.Drawings.Add(newDrawing);
                        DrawingStorageService.Save(_drawingsFile);
                    }

                    _activeDrawingMode = null;
                    SetActiveTool(null);
                    _ = SendDrawingModeToChartAsync("none");
                    _ = SendDrawingsToChartAsync();
                }
            }
            catch { }
            return;
        }

        if (msgType != "crosshair") return;

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
            var last = _currentBars[^1];
            var prev = _currentBars.Count > 1 ? _currentBars[^2] : last;
            SetOhlcHeader(last.Open, last.High, last.Low, last.Close, prev.Open, prev.High, prev.Low, prev.Close);
        }
    }

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

    private void TradingViewLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }

    private string _clockMode = "utc";
    private TimeZoneInfo? _clockZone;

    private void ApplyClockSettings()
    {
        var s = SettingsStorageService.Load();
        _clockMode = s.ClockMode;
        _clockZone = null;

        if (_clockMode == "custom" && !string.IsNullOrEmpty(s.ClockTimeZoneId))
        {
            try { _clockZone = TimeZoneInfo.FindSystemTimeZoneById(s.ClockTimeZoneId); }
            catch { _clockMode = "utc"; }
        }

        ClockZoneText.Text = _clockMode switch
        {
            "system" => "LOCAL",
            "custom" when _clockZone is not null => $"UTC{FormatUtcOffset(_clockZone.BaseUtcOffset)}",
            _ => "UTC",
        };
    }

    private static string FormatUtcOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var abs = offset.Duration();
        return $"{sign}{abs.Hours:00}:{abs.Minutes:00}";
    }

    private void UpdateClockText()
    {
        var now = DateTime.UtcNow;
        var shown = _clockMode switch
        {
            "system" => now.ToLocalTime(),
            "custom" when _clockZone is not null => TimeZoneInfo.ConvertTimeFromUtc(now, _clockZone),
            _ => now,
        };
        ClockText.Text = shown.ToString("HH:mm:ss");
    }

    private void ClockButton_Click(object sender, RoutedEventArgs e)
    {
        if (ClockPopup.IsOpen)
        {
            ClockPopup.IsOpen = false;
            return;
        }
        BuildTimeZoneMenu();
        ClockPopup.IsOpen = true;
    }

    private void BuildTimeZoneMenu()
    {
        TimeZoneListPanel.Children.Clear();

        var zones = TimeZoneInfo.GetSystemTimeZones()
            .OrderBy(z => z.BaseUtcOffset)
            .ThenBy(z => z.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var zone in zones)
        {
            var isSelected = _clockMode == "custom" && _clockZone?.Id == zone.Id;
            var btn = new Button
            {
                Style = (Style)FindResource("TvButtonLeft"),
                Tag = zone.Id,
            };
            btn.Click += TimeZoneItem_Click;
            btn.Content = new TextBlock
            {
                Text = zone.DisplayName,
                FontSize = 11,
                Foreground = isSelected ? (Brush)FindResource("Accent") : (Brush)FindResource("TextSecondary"),
            };
            TimeZoneListPanel.Children.Add(btn);
        }
    }

    private void ClockMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string mode) return;
        var settings = SettingsStorageService.Load();
        settings.ClockMode = mode;
        SettingsStorageService.Save(settings);

        ClockPopup.IsOpen = false;
        ApplyClockSettings();
        UpdateClockText();
    }

    private void TimeZoneItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string zoneId) return;
        var settings = SettingsStorageService.Load();
        settings.ClockMode = "custom";
        settings.ClockTimeZoneId = zoneId;
        SettingsStorageService.Save(settings);

        ClockPopup.IsOpen = false;
        ApplyClockSettings();
        UpdateClockText();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadDashboardAsync();

    private async void OpenBuilderButton_Click(object sender, RoutedEventArgs e)
    {
        var win = new TemplateStoreWindow(_chartSymbol) { Owner = this };
        win.ShowDialog();
        await LoadDashboardAsync();
    }

    private void OpenBacktestButton_Click(object sender, RoutedEventArgs e) => new BacktestWindow().ShowDialog();

    private void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow().ShowDialog();
        StartSignalMonitor();
    }

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

    private void FitTimeframeMenuToWindow()
    {
        var buttonBottomY = TimeframeMenuButton.TranslatePoint(
            new Point(0, TimeframeMenuButton.ActualHeight), this).Y;
        var clientBottomY = (Content as FrameworkElement)?.ActualHeight ?? ActualHeight;

        var available = clientBottomY - buttonBottomY - 6;
        TimeframeMenuScroll.MaxHeight = Math.Clamp(available, 160, 640);
    }

    private void BuildTimeframeMenu()
    {
        TimeframeMenuPanel.Children.Clear();

        foreach (var (group, items) in TimeframeCatalog)
        {
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

    private void CursorGroup_Click(object sender, RoutedEventArgs e)
    {
        LinePopup.IsOpen = false;
        ChannelPopup.IsOpen = false;
        CursorPopup.IsOpen = !CursorPopup.IsOpen;
    }

    private void LineGroup_Click(object sender, RoutedEventArgs e)
    {
        CursorPopup.IsOpen = false;
        LinePopup.IsOpen = false;
        ChannelPopup.IsOpen = false;

        // Highlight the group button that owns this tool
    }

    private void ChannelGroup_Click(object sender, RoutedEventArgs e)
    {
        CursorPopup.IsOpen = false;
        LinePopup.IsOpen = false;
        ChannelPopup.IsOpen = !ChannelPopup.IsOpen;
    }

    private async void ToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;

        var symbolClean = _chartSymbol.Replace("/", "");

        if (tag == "clear")
        {
            var res = MessageBox.Show($"Delete all drawings for {_chartSymbol}?", "Meowgnal", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            _drawingsFile.Drawings.RemoveAll(d => d.Symbol == symbolClean);
            DrawingStorageService.Save(_drawingsFile);
            await SendDrawingsToChartAsync();
            return;
        }

        if (tag == "auto_sr")
        {
            if (_currentBars.Count == 0) return;
            var autoLevels = SupportResistanceDetector.Detect(_chartSymbol, _currentBars);

            _drawingsFile.Drawings.RemoveAll(d => d.Symbol == symbolClean && d.IsAutoDetected);
            _drawingsFile.Drawings.AddRange(autoLevels);
            DrawingStorageService.Save(_drawingsFile);
            await SendDrawingsToChartAsync();
            NotificationService.ShowToast("Meowgnal", $"Detected {autoLevels.Count} important S/R levels.");
            return;
        }

        CursorPopup.IsOpen = false;
        LinePopup.IsOpen = false;

        // Highlight the group button that owns this tool
        var group = tag switch
        {
            "fib" => ToolFibButton,
            "cursor" or "dot" or "arrow" or "eraser" => CursorGroupButton,
            "parallelchannel" or "regressiontrend" or "flattopbottom" or "disjointchannel"
                or "pitchfork" or "schiffpitchfork" or "modifiedschiffpitchfork" or "insidepitchfork"
                => ChannelGroupButton,
            _ => LineGroupButton,
        };

        var mode = tag == "cursor" ? "none" : tag;
        SetActiveTool(tag == "cursor" ? null : group);
        await SendDrawingModeToChartAsync(mode);
    }

    private void SetActiveTool(Button? active)
    {
        var railButtons = new[] { CursorGroupButton, LineGroupButton, ChannelGroupButton, ToolFibButton }; foreach (var b in railButtons)
            b.Background = Brushes.Transparent;

        (active ?? CursorGroupButton).Background = (Brush)FindResource("Accent");
    }

    private async Task SendDrawingModeToChartAsync(string mode)
    {
        try { await _chartPageReady.Task; } catch { return; }
        if (ChartWebView.CoreWebView2 is null) return;

        ChartWebView.CoreWebView2.PostWebMessageAsJson(
            JsonSerializer.Serialize(new { type = "setDrawingMode", mode }));
    }

    private async Task SendDrawingsToChartAsync()
    {
        try { await _chartPageReady.Task; } catch { return; }
        if (ChartWebView.CoreWebView2 is null) return;

        var symbolClean = _chartSymbol.Replace("/", "");
        var drawings = _drawingsFile.Drawings
            .Where(d => d.Symbol == symbolClean)
            .Select(d => new
            {
                id = d.Id,
                kind = d.Kind.ToString().ToLowerInvariant(),
                color = d.Color,
                label = d.Label,
                alert = d.AlertOnCross,
                points = d.Points.Select(p => new { time = p.TimeUnix, price = p.Price }).ToArray()
            }).ToArray();

        ChartWebView.CoreWebView2.PostWebMessageAsJson(
            JsonSerializer.Serialize(new { type = "setDrawings", drawings }));
    }

    private void ChartTypeButton_Click(object sender, RoutedEventArgs e) =>
        ChartTypePopup.IsOpen = !ChartTypePopup.IsOpen;

    private void ChartTypeItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string type) return;
        ChartTypePopup.IsOpen = false;
        ApplyChartType(type);
        _ = SendChartTypeAsync(type);
    }

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

    private async Task UpdateChartAsync(List<Bar> bars)
    {
        _currentBars = bars;

        var last = bars[^1];
        var prev = bars.Count > 1 ? bars[^2] : last;
        SetOhlcHeader(last.Open, last.High, last.Low, last.Close, prev.Open, prev.High, prev.Low, prev.Close);

        await SendCandlesToChartAsync(bars);
        _ = SendPositionsToChartAsync();
        _ = SendDrawingsToChartAsync();
        _ = SendThemeToChartAsync();
    }

    private async Task SendCandlesToChartAsync(List<Bar> bars)
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

        var payload = new
        {
            type = "setCandles",
            secondsVisible = _chartTimeframe.EndsWith('s'),
            data = bars.Select(b => new
            {
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

    private sealed record FoundSignal(StrategyDefinition Strategy, SignalEvent Signal);

    private static string MakeSignalKey(string strategyId, SignalEvent signal) =>
        $"{strategyId}|{signal.Timestamp:O}|{(int)signal.Type}";

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
        if (_isScanning) return;
        _isScanning = true;
        try
        {
            var settings = SettingsStorageService.Load();
            var found = await ScanAllStrategiesAsync();

            if (!_baselineSeeded)
            {
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
                foreach (var f in fresh.Take(5))
                {
                    NotificationService.ShowToast(
                        $"Meowgnal — {f.Strategy.Symbol} ({f.Strategy.Timeframe})",
                        $"{(f.Signal.Type == SignalType.Entry ? "BUY" : "SELL")} signal: {f.Strategy.Name}");
                }
            }

            if (settings.SoundNotificationsEnabled)
                NotificationService.PlayAlertSound();

            if (settings.PaperAutoTradeEnabled)
                await AutoTradeSignalsAsync(fresh, settings);
        }
        catch
        {
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
            }
        }
        return found;
    }
}