using Meowgnal.DataProviders;
using Meowgnal.Engine;
using Meowgnal.Models;
using Meowgnal.Services;
using Meowgnal.Views;
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
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Drawing = Meowgnal.Models.Drawing;

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
    private readonly DrawingUndoManager _undoManager = new();
    private string? _activeDrawingMode = null;
    private string _activeCursorMode = "cross";
    private string? _pendingStickerLabel = null;
    private int _pendingStickerFontSize = 22;
    private string _pendingStickerFontFamily = "Segoe UI Emoji";

    // Price alerts state
    private PriceAlertsFile _alerts = new();
    private readonly Dictionary<string, bool?> _drawingAlertWasAbove = new();

    // Replay mode state
    private bool _replayMode;
    private List<Bar> _replayBars = new();
    private int _replayShown;
    private readonly DispatcherTimer _replayTimer = new() { Interval = TimeSpan.FromSeconds(1) };

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
        ThemeService.ApplyTheme(SettingsStorageService.Load());

        var splash = new SplashWindow();
        splash.Show();

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 1200)
        {
            Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
        }

        InitializeComponent();

        var v = Assembly.GetEntryAssembly()?.GetName().Version;
        TitleBarVersion.Text = v is null ? "" : $"v{v.Major}.{v.Minor}";
        splash.Close();

        SignalsList.ItemsSource = _signals;
        ChartWebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0x13, 0x17, 0x22);
        ApplyChartType("candles");
        SetActiveTool(null);
        UpdateCursorButtonIcon();
        UpdateKeepDrawingButtonVisual();
        UpdateMagnetButtonVisual();
        UpdateLockAllDrawingsButtonVisual();
        LongPressTooltipToggle.IsChecked = SettingsStorageService.Load().LongPressTooltipEnabled;
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
        IndicatorPanelControl.IndicatorSelected += AddIndicatorToChart;
        _replayTimer.Tick += (_, _) => ReplayStep(1);
        Loaded += MainWindow_Loaded;
        PreviewKeyDown += UndoRedo_KeyDown;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = SettingsStorageService.Load();

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
                }
                SettingsStorageService.Save(settings);
            }
            else
            {
                Application.Current.Shutdown();
                return;
            }
        }

        UpdateProfileMenu(settings);
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
        _ = SendDrawingsToChartAsync();
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
        UpdateLearningPathBanner();
        StartSignalMonitor();
    }

    private void UpdateProfileMenu(AppSettings settings)
    {
        ProfileAvatarText.Text = settings.ProfileAvatar;
        MenuAvatarText.Text = settings.ProfileAvatar;
        MenuNameText.Text = settings.ProfileName;
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
        new WhatsNewWindow { Owner = this }.ShowDialog();
    }

    private void MenuAcademy_Click(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        new IndicatorAcademyWindow { Owner = this }.ShowDialog();
    }

    private void MenuSignOut_Click(object sender, RoutedEventArgs e)
    {
        ProfilePopup.IsOpen = false;
        var res = MessageBox.Show("Sign out and switch profile? This will restart the app.", "Meowgnal", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            var settings = SettingsStorageService.Load();
            settings.FirstRunCompleted = false;
            SettingsStorageService.Save(settings);
            Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }
    }

    #region Learning Path

    private void UpdateLearningPathBanner()
    {
        var settings = SettingsStorageService.Load();
        LearningPathBanner.Visibility = settings.LearningPathStepCompleted < 4
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void LearningPathBanner_Click(object sender, MouseButtonEventArgs e)
    {
        var win = new Views.LearningPathWindow { Owner = this };
        win.Show();
    }

    #endregion

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
            sp.Children.Add(MakeCoinBadge(tab.Symbol));
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
        if (_replayMode) ForceExitReplayUi();
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
        RebuildObjectList();
        ObjectsSymbolText.Text = _chartSymbol;
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

    private ContentControl MakeCoinBadge(string symbol)
    {
        var coin = (symbol ?? "").Split('/')[0].ToUpperInvariant();
        var host = new ContentControl
        {
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Content = new TextBlock { Text = "🪙", FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
        };
        if (CoinLogoService.TryGetCached(coin, out var cached))
            host.Content = MakeLogoImage(cached);
        else
            _ = LoadLogoAsync(host, coin);
        return host;
    }

    private static Image MakeLogoImage(BitmapImage source)
    {
        var img = new Image { Source = source, Width = 16, Height = 16 };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        return img;
    }

    private async Task LoadLogoAsync(ContentControl host, string coin)
    {
        try
        {
            var img = await CoinLogoService.LoadAsync(coin);
            if (img is null) return;
            host.Dispatcher.Invoke(() => host.Content = MakeLogoImage(img));
        }
        catch
        {
            // Keep the fallback badge when offline or unknown coin.
        }
    }

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

            var symPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            symPanel.Children.Add(MakeCoinBadge(item.Symbol));
            symPanel.Children.Add(new TextBlock
            {
                Text = item.Symbol,
                Foreground = (Brush)FindResource("TextPrimary"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
            });

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

            row.Children.Add(symPanel);
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

        _watchTimer.Stop();
        ChecklistResult checklistResult = new();
        try
        {
            var checklistPrompt = new ChecklistPromptWindow(settings.DefaultChecklist) { Owner = this };
            if (checklistPrompt.ShowDialog() != true)
            {
                _watchTimer.Start();
                return;
            }
            checklistResult = checklistPrompt.Result.Result!;
        }
        finally
        {
            _watchTimer.Start();
        }

        var result = PaperTradingEngine.TryOpen(
            _paperAccount, settings, symbol, dataSource, side, entry, leverage,
            slPrice, tpPrice, trailing, trailDist, trailAct, marginUsdt, strategyId: null);

        if (!result.Ok)
        {
            NotificationService.ShowToast("Meowgnal", result.Error);
            return;
        }

        result.Position!.ChecklistResult = checklistResult;
        SavePaperAccount();
        OpenPositionPopup.IsOpen = false;
        RebuildPaperPanel();
        _ = SendPositionsToChartAsync();
        NotificationService.ShowToast("Meowgnal",
            $"{(side == PositionSide.Long ? "LONG" : "SHORT")} {symbol} opened: {result.Position!.Size} @ {FormatPrice(entry)} " +
            $"(margin {result.Position.Margin:N2} USDT, {leverage:0}x)");
        NotificationService.NotifyPaperEvent($"{(side == PositionSide.Long ? "LONG" : "SHORT")} opened", symbol, entry);
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
        NotificationService.NotifyPaperEvent($"Closed ({trade.Reason})", trade.Symbol, ticker.Last);
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

        var positions = _paperAccount.OpenPositions.ToList();
        if (positions.Count == 0)
        {
            UpdatePaperSummary(new Dictionary<string, decimal>());
            return;
        }

        var prices = new Dictionary<string, decimal>();
        foreach (var group in positions.GroupBy(p => p.DataSource).ToList())
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
        foreach (var symbol in positions.Select(p => p.Symbol).Distinct())
        {
            try
            {
                var src = positions.First(p => p.Symbol == symbol).DataSource;
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
        foreach (var pos in positions)
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
            foreach (var pos in positions)
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
        foreach (var trade in closedTrades)
            NotificationService.NotifyPaperEvent($"Closed ({trade.Reason})", trade.Symbol);

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
        if (_alerts.Alerts.Count > 0)
        {
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
        await CheckDrawingAlertsAsync();
    }

    private async Task CheckDrawingAlertsAsync()
    {
        var horizontalDrawings = _drawingsFile.Drawings
            .Where(d => d.AlertOnCross &&
                        (d.Kind == DrawingKind.HorizontalLine ||
                         d.Kind == DrawingKind.HorizontalRay ||
                         d.Kind == DrawingKind.PriceLabel) &&
                        !string.IsNullOrWhiteSpace(d.Symbol) &&
                        d.Points.Count > 0)
            .ToList();
        if (horizontalDrawings.Count == 0) return;

        foreach (var group in horizontalDrawings.GroupBy(d => d.DataSource).ToList())
        {
            try
            {
                IDataProvider provider = group.Key == "hyperliquid"
                    ? new HyperliquidDataProvider()
                    : new BinanceDataProvider();
                var symbols = group.Select(d => d.Symbol).Distinct();
                var tickers = await provider.GetTickersAsync(symbols);
                foreach (var drawing in group)
                {
                    if (!tickers.TryGetValue(drawing.Symbol, out var t)) continue;
                    var alertPrice = GetAlertPrice(drawing);
                    if (alertPrice <= 0) continue;
                    var above = t.Last >= alertPrice;
                    if (!_drawingAlertWasAbove.TryGetValue(drawing.Id, out var wasAbove) || wasAbove is null)
                    {
                        _drawingAlertWasAbove[drawing.Id] = above;
                        continue;
                    }
                    if (above != wasAbove)
                    {
                        _drawingAlertWasAbove[drawing.Id] = above;
                        drawing.AlertOnCross = false;
                        DrawingStorageService.Save(_drawingsFile);
                        _ = SendDrawingsToChartAsync();
                        RebuildObjectList();
                        var kindName = KindLabel(drawing.Kind);
                        NotificationService.ShowToast($"Meowgnal — {drawing.Symbol}",
                            $"🔔 {kindName} crossed {alertPrice:N2} (now {t.Last:N2})");
                        if (SettingsStorageService.Load().SoundNotificationsEnabled)
                            NotificationService.PlayAlertSound();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error checking drawing alerts for {group.Key}", ex);
            }
        }
    }

    private static decimal GetAlertPrice(Drawing drawing)
    {
        if (drawing.Points.Count == 0) return 0m;
        return drawing.Kind switch
        {
            DrawingKind.HorizontalLine => drawing.Points[0].Price,
            DrawingKind.HorizontalRay => drawing.Points[0].Price,
            DrawingKind.PriceLabel => drawing.Points[0].Price,
            _ => drawing.Points[0].Price
        };
    }

    private static bool IsStrategyInPortfolio(StrategyDefinition strategy, AppSettings settings)
    {
        if (settings.PortfolioEnabledStrategyIds.Count == 0) return true;
        return settings.PortfolioEnabledStrategyIds.Contains(strategy.StrategyId);
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
                if (!IsStrategyInPortfolio(f.Strategy, settings)) continue;
                if (settings.PortfolioMaxTotalPositions > 0 &&
                    _paperAccount.OpenPositions.Count >= settings.PortfolioMaxTotalPositions)
                    continue;
                if (settings.PortfolioMaxPositionsPerStrategy > 0 &&
                    _paperAccount.OpenPositions.Count(p => p.StrategyId == f.Strategy.StrategyId) >= settings.PortfolioMaxPositionsPerStrategy)
                    continue;

                _watchTimer.Stop();
                ChecklistResult? checklistResult = null;
                try
                {
                    var checklist = f.Strategy.CustomChecklist ?? settings.DefaultChecklist;
                    var checklistPrompt = new ChecklistPromptWindow(checklist) { Owner = this };
                    if (checklistPrompt.ShowDialog() == true)
                        checklistResult = checklistPrompt.Result.Result;
                }
                finally
                {
                    _watchTimer.Start();
                }
                if (checklistResult is null) continue;

                var barsForSnapshot = await provider.GetHistoricalCandlesAsync(symbol, f.Strategy.Timeframe, limit: 50);
                var entrySnapshot = new Dictionary<string, decimal>();
                var entryExplanation = "Auto-opened by signal.";
                if (barsForSnapshot.Count > 0)
                {
                    var series = RuleEngine.CalculateIndicatorSeries(barsForSnapshot, f.Strategy.Indicators);
                    var lastIdx = barsForSnapshot.Count - 1;
                    entrySnapshot = RuleEngine.CaptureSnapshot(
                        f.Strategy.EntryRules.Conditions, lastIdx, barsForSnapshot, series);
                    entryExplanation = StrategyDescriptionService.DescribeTradeEntry(f.Strategy, entrySnapshot);
                }

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
                    customMarginUsdt: 0m, strategyId: f.Strategy.StrategyId,
                    entrySnapshot: entrySnapshot, entryExplanation: entryExplanation);

                if (result.Ok)
                {
                    result.Position!.ChecklistResult = checklistResult;
                    changed = true;
                    NotificationService.ShowToast("Meowgnal — auto trade",
                        $"AUTO LONG {symbol} @ {FormatPrice(price)} (margin {result.Position!.Margin:N2} USDT) by {f.Strategy.Name}");
                    NotificationService.NotifyPaperEvent($"AUTO LONG by {f.Strategy.Name}", symbol, price);
                }
            }
            else
            {
                var pos = _paperAccount.OpenPositions.FirstOrDefault(p => p.Symbol == symbol);
                if (pos is null) continue;
                var trade = PaperTradingEngine.Close(_paperAccount, pos, price, CloseReason.SignalExit, settings.PaperTakerFeePercent, f.Strategy);
                changed = true;
                CheckDailySuspension(settings);
                NotificationService.ShowToast("Meowgnal — auto trade",
                    $"AUTO CLOSE {trade.Symbol}: PnL {trade.PnL:+0.00;-0.00} USDT ({f.Strategy.Name})");
                NotificationService.NotifyPaperEvent($"AUTO CLOSE ({f.Strategy.Name})", trade.Symbol, price);
            }
        }

        if (changed)
        {
            SavePaperAccount();
            RebuildPaperPanel();
            _ = SendPositionsToChartAsync();
        }
    }

    public List<string> GetWatchlistSymbols()
    {
        return _activeWatchlist.Items.Select(i => i.Symbol).ToList();
    }

    public async Task<bool> OpenPaperPositionFromDrawingAsync(string side, decimal entry, decimal sl, decimal tp, decimal sizePercent)
    {
        var settings = SettingsStorageService.Load();
        var posSide = side == "long" ? PositionSide.Long : PositionSide.Short;
        var marginUsdt = _paperAccount.CurrentBalance * Math.Clamp(sizePercent, 1m, 100m) / 100m;
        var result = PaperTradingEngine.TryOpen(
            _paperAccount, settings, _chartSymbol, _chartDataSource, posSide, entry,
            settings.PaperDefaultLeverage, sl, tp,
            trailingEnabled: false, trailingDistancePercent: 0m, trailingActivationPercent: 0m,
            customMarginUsdt: marginUsdt,
            entryExplanation: "Opened from Long/Short Position drawing tool.");
        if (!result.Ok)
        {
            NotificationService.ShowToast("Meowgnal", result.Error);
            return false;
        }
        SavePaperAccount();
        RebuildPaperPanel();
        await SendPositionsToChartAsync();
        NotificationService.ShowToast("Meowgnal",
            $"{(posSide == PositionSide.Long ? "LONG" : "SHORT")} {_chartSymbol} opened from drawing: {result.Position!.Size} @ {FormatPrice(entry)}");
        return true;
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
            core.Settings.AreDevToolsEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.NavigationCompleted += (_, _) =>
            {
                _chartPageReady.TrySetResult(true);
                _ = SendDrawingsToChartAsync();
            };
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
                "Please download and install this small official package from Microsoft, then run the app again:\n" +
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
                CaptureSnapshot();
                _drawingsFile.Drawings.RemoveAll(d => d.Id == id);
                DrawingStorageService.Save(_drawingsFile);
                _ = SendDrawingsToChartAsync();
                RebuildObjectList();
            }
            return;
        }

        if (msgType == "deleteDrawings")
        {
            if (root.TryGetProperty("ids", out var idsProp) && idsProp.ValueKind == JsonValueKind.Array)
            {
                var idsToDelete = new HashSet<string>();
                foreach (var idEl in idsProp.EnumerateArray())
                {
                    var idStr = idEl.GetString();
                    if (!string.IsNullOrEmpty(idStr)) idsToDelete.Add(idStr);
                }
                if (idsToDelete.Count > 0)
                {
                    CaptureSnapshot();
                    _drawingsFile.Drawings.RemoveAll(d => idsToDelete.Contains(d.Id));
                    DrawingStorageService.Save(_drawingsFile);
                    _ = SendDrawingsToChartAsync();
                    RebuildObjectList();
                }
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

                    var newDrawing = new Drawing { Kind = kind, Symbol = _chartSymbol.Replace("/", ""), DataSource = _chartDataSource };

                    if (drawingEl.TryGetProperty("label", out var tLabelEl))
                        newDrawing.Label = tLabelEl.GetString() ?? "";
                    if (drawingEl.TryGetProperty("color", out var tColorEl))
                        newDrawing.Color = tColorEl.GetString() ?? "#2962FF";
                    if (kind == DrawingKind.Sticker && _pendingStickerLabel is not null)
                    {
                        newDrawing.Label = _pendingStickerLabel;
                        newDrawing.FontSize = _pendingStickerFontSize;
                        newDrawing.FontFamily = _pendingStickerFontFamily;
                        _pendingStickerLabel = null;
                    }

                    if (kind is DrawingKind.Pitchfork or DrawingKind.SchiffPitchfork
                        or DrawingKind.ModifiedSchiffPitchfork or DrawingKind.InsidePitchfork)
                        newDrawing.ExtendRight = true;

                    if (kind is DrawingKind.ElliottImpulseWave or DrawingKind.ElliottCorrectionWave
                        or DrawingKind.ElliottTriangleWave or DrawingKind.ElliottDoubleComboWave
                        or DrawingKind.ElliottTripleComboWave)
                    {
                        newDrawing.ShowRatios = false;
                        newDrawing.ShowApex = false;
                        newDrawing.ShowLabels = true;
                    }

                    if (kind is DrawingKind.CyclicLines or DrawingKind.TimeCycles)
                    {
                        newDrawing.ShowRatios = false;
                        newDrawing.ShowApex = false;
                        newDrawing.ShowLabels = kind == DrawingKind.TimeCycles;
                    }

                    if (kind == DrawingKind.SineLine)
                    {
                        newDrawing.ShowRatios = false;
                        newDrawing.ShowApex = false;
                        newDrawing.ShowLabels = false;
                    }

                    if (kind is DrawingKind.LongPosition or DrawingKind.ShortPosition)
                    {
                        newDrawing.PositionSide = kind == DrawingKind.LongPosition ? "long" : "short";
                        newDrawing.ShowRatios = false;
                        newDrawing.ShowLabels = true;
                    }

                    if (kind == DrawingKind.PositionForecast)
                    {
                        newDrawing.LineStyle = "dashed";
                        newDrawing.ShowRatios = false;
                        newDrawing.ShowLabels = false;
                    }

                    if (kind == DrawingKind.BarsPattern)
                    {
                        newDrawing.ShowRatios = false;
                        newDrawing.ShowLabels = false;
                    }

                    if (kind == DrawingKind.GhostFeed)
                    {
                        newDrawing.ShowRatios = false;
                        newDrawing.ShowLabels = false;
                    }

                    if (kind == DrawingKind.Sector)
                    {
                        newDrawing.ShowRatios = false;
                        newDrawing.ShowApex = false;
                        newDrawing.ShowLabels = false;
                    }
                    if (kind is DrawingKind.AnchoredVwap or DrawingKind.FixedRangeVolumeProfile or DrawingKind.AnchoredVolumeProfile
                    or DrawingKind.PriceRange or DrawingKind.DateRange or DrawingKind.DateAndPriceRange)
                    {
                        newDrawing.ShowRatios = false;
                        newDrawing.ShowApex = false;
                        newDrawing.ShowLabels = true;
                    }

                    if (drawingEl.TryGetProperty("points", out var pts))
                    {
                        newDrawing.SecondLineColor = newDrawing.Color;
                        foreach (var pt in pts.EnumerateArray())
                        {
                            newDrawing.Points.Add(new DrawingPoint
                            {
                                TimeUnix = pt.GetProperty("time").GetInt64(),
                                Price = pt.GetProperty("price").GetDecimal(),
                            });
                        }
                    }

                    if (kind is DrawingKind.LongPosition or DrawingKind.ShortPosition)
                    {
                        if (newDrawing.Points.Count >= 1)
                            newDrawing.EntryPrice = (decimal)newDrawing.Points[0].Price;
                        if (newDrawing.Points.Count >= 2)
                            newDrawing.StopLossPrice = (decimal)newDrawing.Points[1].Price;
                        if (newDrawing.EntryPrice > 0 && newDrawing.StopLossPrice > 0)
                        {
                            var risk = Math.Abs(newDrawing.EntryPrice - newDrawing.StopLossPrice);
                            newDrawing.TakeProfitPrice = kind == DrawingKind.LongPosition
                                ? newDrawing.EntryPrice + 2 * risk
                                : newDrawing.EntryPrice - 2 * risk;
                        }
                    }

                    if (kind is DrawingKind.CyclicLines or DrawingKind.TimeCycles)
                    {
                        if (newDrawing.Points.Count >= 2)
                            newDrawing.CycleIntervalSeconds = Math.Abs(newDrawing.Points[1].TimeUnix - newDrawing.Points[0].TimeUnix);
                    }

                    if (newDrawing.Points.Count > 0)
                    {
                        CaptureSnapshot();
                        _drawingsFile.Drawings.Add(newDrawing);
                        DrawingStorageService.Save(_drawingsFile);
                    }

                    if (!SettingsStorageService.Load().KeepDrawingEnabled)
                    {
                        _activeDrawingMode = null;
                        SetActiveTool(null);
                        _ = SendDrawingModeToChartAsync("none");
                    }
                    _ = SendDrawingsToChartAsync();
                    RebuildObjectList();
                }
            }
            catch { }
            return;
        }

        if (msgType == "captureSnapshot")
        {
            CaptureSnapshot();
            return;
        }

        if (msgType == "updateDrawing")
        {
            try
            {
                if (root.TryGetProperty("drawing", out var drawingEl) &&
                    drawingEl.TryGetProperty("id", out var idEl))
                {
                    var id = idEl.GetString();
                    var existing = _drawingsFile.Drawings.FirstOrDefault(d => d.Id == id);
                    if (existing is not null && !existing.IsLocked &&
                        drawingEl.TryGetProperty("points", out var pts))
                    {
                        var newPoints = new List<DrawingPoint>();
                        foreach (var pt in pts.EnumerateArray())
                        {
                            newPoints.Add(new DrawingPoint
                            {
                                TimeUnix = pt.GetProperty("time").GetInt64(),
                                Price = pt.GetProperty("price").GetDecimal()
                            });
                        }
                        if (newPoints.Count > 0)
                        {
                            existing.Points = newPoints;
                            if (drawingEl.TryGetProperty("groupId", out var gidEl))
                                existing.GroupId = gidEl.ValueKind == JsonValueKind.Null ? null : gidEl.GetString();
                            if (drawingEl.TryGetProperty("zIndex", out var ziEl))
                                existing.ZIndex = ziEl.GetInt32();
                            if (drawingEl.TryGetProperty("fontSize", out var fsEl))
                                existing.FontSize = fsEl.GetInt32();
                            if (drawingEl.TryGetProperty("fontFamily", out var ffEl))
                                existing.FontFamily = ffEl.GetString() ?? "Trebuchet MS";
                            if (drawingEl.TryGetProperty("gannRatios", out var grEl) && grEl.ValueKind == JsonValueKind.Array)
                            {
                                existing.GannRatios = new List<double>();
                                foreach (var rEl in grEl.EnumerateArray())
                                    existing.GannRatios.Add(rEl.GetDouble());
                            }
                            DrawingStorageService.Save(_drawingsFile);
                            _ = SendDrawingsToChartAsync();
                            RebuildObjectList();
                        }
                    }
                }
            }
            catch { }
            return;
        }

        if (msgType == "updateDrawings")
        {
            try
            {
                if (root.TryGetProperty("drawings", out var drawingsEl) && drawingsEl.ValueKind == JsonValueKind.Array)
                {
                    CaptureSnapshot();
                    foreach (var drawingEl in drawingsEl.EnumerateArray())
                    {
                        if (!drawingEl.TryGetProperty("id", out var idEl)) continue;
                        var id = idEl.GetString();
                        var existing = _drawingsFile.Drawings.FirstOrDefault(d => d.Id == id);
                        if (existing is null || existing.IsLocked) continue;
                        if (drawingEl.TryGetProperty("points", out var pts))
                        {
                            var newPoints = new List<DrawingPoint>();
                            foreach (var pt in pts.EnumerateArray())
                            {
                                newPoints.Add(new DrawingPoint
                                {
                                    TimeUnix = pt.GetProperty("time").GetInt64(),
                                    Price = pt.GetProperty("price").GetDecimal()
                                });
                            }
                            if (newPoints.Count > 0) existing.Points = newPoints;
                        }
                    }
                    DrawingStorageService.Save(_drawingsFile);
                    _ = SendDrawingsToChartAsync();
                }
            }
            catch { }
            return;
        }

        if (msgType == "groupDrawings")
        {
            try
            {
                if (root.TryGetProperty("ids", out var idsEl) && idsEl.ValueKind == JsonValueKind.Array)
                {
                    var groupId = root.TryGetProperty("groupId", out var gidEl) ? gidEl.GetString() : Guid.NewGuid().ToString("N");
                    if (string.IsNullOrEmpty(groupId)) groupId = Guid.NewGuid().ToString("N");
                    CaptureSnapshot();
                    var idSet = new HashSet<string>();
                    foreach (var idEl in idsEl.EnumerateArray())
                    {
                        var idStr = idEl.GetString();
                        if (!string.IsNullOrEmpty(idStr)) idSet.Add(idStr);
                    }
                    foreach (var d in _drawingsFile.Drawings)
                    {
                        if (idSet.Contains(d.Id)) d.GroupId = groupId;
                    }
                    DrawingStorageService.Save(_drawingsFile);
                    _ = SendDrawingsToChartAsync();
                }
            }
            catch { }
            return;
        }

        if (msgType == "ungroupDrawings")
        {
            try
            {
                if (root.TryGetProperty("ids", out var idsEl) && idsEl.ValueKind == JsonValueKind.Array)
                {
                    CaptureSnapshot();
                    var idSet = new HashSet<string>();
                    foreach (var idEl in idsEl.EnumerateArray())
                    {
                        var idStr = idEl.GetString();
                        if (!string.IsNullOrEmpty(idStr)) idSet.Add(idStr);
                    }
                    foreach (var d in _drawingsFile.Drawings)
                    {
                        if (idSet.Contains(d.Id)) d.GroupId = null;
                    }
                    DrawingStorageService.Save(_drawingsFile);
                    _ = SendDrawingsToChartAsync();
                }
            }
            catch { }
            return;
        }

        if (msgType == "openDrawingProperties")
        {
            var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            if (!string.IsNullOrEmpty(id)) OnOpenDrawingProperties(id);
            return;
        }

        if (msgType == "copyToast")
        {
            return;
        }
        if (msgType == "zoomFinished")
        {
            _activeDrawingMode = null;
            SetActiveTool(null);
            return;
        }
        if (msgType == "escapePressed")
        {
            if (_activeDrawingMode is not null)
            {
                _activeDrawingMode = null;
                SetActiveTool(null);
                _ = SendDrawingModeToChartAsync("none");
            }
            return;
        }
        if (msgType == "measureFinished")
        {
            _activeDrawingMode = null;
            SetActiveTool(null);
            return;
        }

        if (msgType == "pasteDrawing")
        {
            try
            {
                if (root.TryGetProperty("drawing", out var drawingEl))
                {
                    var kindStr = drawingEl.TryGetProperty("kind", out var k) ? k.GetString() : "horizontal";
                    var kind = Enum.TryParse<DrawingKind>(kindStr, true, out var parsedKind) ? parsedKind : DrawingKind.HorizontalLine;
                    var newDrawing = new Drawing { Kind = kind, Symbol = _chartSymbol.Replace("/", ""), DataSource = _chartDataSource };
                    if (drawingEl.TryGetProperty("label", out var labelEl))
                        newDrawing.Label = labelEl.GetString() ?? "";
                    if (drawingEl.TryGetProperty("color", out var colorEl))
                        newDrawing.Color = colorEl.GetString() ?? "#2962FF";
                    if (drawingEl.TryGetProperty("id", out var idEl) && idEl.GetString() is { Length: > 0 } newId)
                        newDrawing.Id = newId;
                    if (drawingEl.TryGetProperty("color", out var cEl))
                        newDrawing.Color = cEl.GetString() ?? newDrawing.Color;
                    if (drawingEl.TryGetProperty("label", out var lEl))
                        newDrawing.Label = lEl.GetString() ?? "";
                    if (drawingEl.TryGetProperty("points", out var pts))
                    {
                        foreach (var pt in pts.EnumerateArray())
                        {
                            newDrawing.Points.Add(new DrawingPoint
                            {
                                TimeUnix = pt.GetProperty("time").GetInt64(),
                                Price = pt.GetProperty("price").GetDecimal()
                            });
                        }
                    }
                    if (newDrawing.Points.Count > 0)
                    {
                        CaptureSnapshot();
                        _drawingsFile.Drawings.Add(newDrawing);
                        DrawingStorageService.Save(_drawingsFile);
                        _ = SendDrawingsToChartAsync();
                        RebuildObjectList();
                    }
                }
            }
            catch { }
            return;
        }

        if (msgType == "removeIndicator")
        {
            var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
            var list = GetActiveIndicators(_chartSymbol);
            var item = list.FirstOrDefault(x => x.Type == id);
            if (item is not null)
            {
                list.Remove(item);
                IndicatorSettingsStorageService.Save(_indicatorSettings);
            }
            IndicatorPanelControl.RefreshActiveTypes(list.Select(x => x.Type));
            return;
        }

        if (msgType == "updateIndicator")
        {
            var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
            var period = root.TryGetProperty("period", out var pProp) ? pProp.GetInt32() : 14;
            var color = root.TryGetProperty("color", out var cProp) ? cProp.GetString() : null;
            var width = root.TryGetProperty("lineWidth", out var wProp) ? wProp.GetInt32() : 2;
            var list = GetActiveIndicators(_chartSymbol);
            var item = list.FirstOrDefault(x => x.Type == id);
            if (item is not null)
            {
                item.Period = period;
                item.Color = string.IsNullOrWhiteSpace(color) ? null : color;
                item.LineWidth = width;
                IndicatorSettingsStorageService.Save(_indicatorSettings);
                _ = RefreshIndicatorsOnChartAsync();
            }
            return;
        }

        if (msgType == "reorderIndicators")
        {
            if (root.TryGetProperty("order", out var orderProp) && orderProp.ValueKind == JsonValueKind.Array)
            {
                var order = orderProp.EnumerateArray().Select(x => x.GetString() ?? "").ToList();
                var list = GetActiveIndicators(_chartSymbol);
                var reordered = order
                    .Select(id => list.FirstOrDefault(x => x.Type == id))
                    .Where(x => x is not null)
                    .Cast<ActiveIndicator>()
                    .ToList();
                foreach (var item in list.Where(x => !order.Contains(x.Type)))
                    reordered.Add(item);
                list.Clear();
                foreach (var x in reordered) list.Add(x);
                IndicatorSettingsStorageService.Save(_indicatorSettings);
            }
            return;
        }

        if (msgType == "updateTableData")
        {
            var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            var tableData = root.TryGetProperty("tableData", out var tdProp) ? tdProp.GetString() : "[]";
            if (!string.IsNullOrEmpty(id))
            {
                var drawing = _drawingsFile.Drawings.FirstOrDefault(d => d.Id == id);
                if (drawing != null)
                {
                    drawing.TableData = tableData;
                    DrawingStorageService.Save(_drawingsFile);
                }
            }
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
        var win = new StrategyManagerWindow(_chartSymbol) { Owner = this };
        win.ShowDialog();
        await LoadDashboardAsync();
    }

    private void OpenBacktestButton_Click(object sender, RoutedEventArgs e) => new BacktestWindow().ShowDialog();
    private void OpenJournalButton_Click(object sender, RoutedEventArgs e) => new JournalWindow().ShowDialog();
    private void OpenPortfolioButton_Click(object sender, RoutedEventArgs e) => new PortfolioWindow().ShowDialog();
    private void ColorPickerButton_Click(object sender, RoutedEventArgs e) => ColorPopup.IsOpen = !ColorPopup.IsOpen;

    private async void DefaultColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string hex) return;
        _drawingsFile.DefaultColor = hex;
        DrawingStorageService.Save(_drawingsFile);
        ColorPopup.IsOpen = false;
        await SendDrawingModeToChartAsync(_activeDrawingMode ?? "none");
        NotificationService.ShowToast("Meowgnal", $"New drawings will use color {hex}.");
    }

    private void TrashButton_Click(object sender, RoutedEventArgs e) => TrashPopup.IsOpen = true;

    private async void ToggleDrawingsVisibility_Click(object sender, RoutedEventArgs e)
    {
        var settings = SettingsStorageService.Load();
        settings.DrawingsHidden = !settings.DrawingsHidden;
        SettingsStorageService.Save(settings);
        await SendDrawingsToChartAsync();
    }

    private void ClearAllDrawings_Click(object sender, RoutedEventArgs e)
    {
        CaptureSnapshot();
        var symbolClean = _chartSymbol.Replace("/", "");
        _drawingsFile.Drawings.RemoveAll(d => d.Symbol == symbolClean);
        DrawingStorageService.Save(_drawingsFile);
        _ = SendDrawingsToChartAsync();
        RebuildObjectList();
    }

    private void ChooseDeleteDrawings_Click(object sender, RoutedEventArgs e)
    {
        TrashPopup.IsOpen = false;
        RebuildObjectList();
        ObjectsPopup.IsOpen = true;
    }

    private async void OnOpenDrawingProperties(string id)
    {
        try
        {
            var drawing = _drawingsFile.Drawings.FirstOrDefault(d => d.Id == id);
            if (drawing is null) return;
            CaptureSnapshot();
            var win = new DrawingPropertiesWindow(drawing) { Owner = this };
            if (win.ShowDialog() == true)
            {
                DrawingStorageService.Save(_drawingsFile);
                await SendDrawingsToChartAsync();
                RebuildObjectList();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Fatal("Error opening drawing properties", ex);
            MessageBox.Show(
                "Error opening drawing properties:\n" + ex.Message,
                "Meowgnal",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

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
        if (_replayMode) ForceExitReplayUi();
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
        if (_replayMode) ForceExitReplayUi();
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
        LinePopup.IsOpen = false; FibPopup.IsOpen = false; PatternsPopup.IsOpen = false; BrushesShapesPopup.IsOpen = false; TextNotesPopup.IsOpen = false; ForecastPopup.IsOpen = false; IconStampPopup.IsOpen = false;
        if (!CursorPopup.IsOpen) RefreshCursorMenuHighlight();
        CursorPopup.IsOpen = !CursorPopup.IsOpen;
    }

    private void LineGroup_Click(object sender, RoutedEventArgs e)
    {
        CursorPopup.IsOpen = false; FibPopup.IsOpen = false; PatternsPopup.IsOpen = false; BrushesShapesPopup.IsOpen = false; TextNotesPopup.IsOpen = false; ForecastPopup.IsOpen = false; IconStampPopup.IsOpen = false;
        LinePopup.IsOpen = !LinePopup.IsOpen;
    }

    private void FibGroup_Click(object sender, RoutedEventArgs e)
    {
        CursorPopup.IsOpen = false; LinePopup.IsOpen = false; PatternsPopup.IsOpen = false; BrushesShapesPopup.IsOpen = false; TextNotesPopup.IsOpen = false; ForecastPopup.IsOpen = false; IconStampPopup.IsOpen = false;
        FibPopup.IsOpen = !FibPopup.IsOpen;
    }

    private void PatternsGroup_Click(object sender, RoutedEventArgs e)
    {
        CursorPopup.IsOpen = false; LinePopup.IsOpen = false; FibPopup.IsOpen = false; BrushesShapesPopup.IsOpen = false; TextNotesPopup.IsOpen = false; ForecastPopup.IsOpen = false; IconStampPopup.IsOpen = false;
        PatternsPopup.IsOpen = !PatternsPopup.IsOpen;
    }

    private void BrushesShapesGroup_Click(object sender, RoutedEventArgs e)
    {
        CursorPopup.IsOpen = false; LinePopup.IsOpen = false; FibPopup.IsOpen = false; PatternsPopup.IsOpen = false; TextNotesPopup.IsOpen = false; ForecastPopup.IsOpen = false; IconStampPopup.IsOpen = false;
        BrushesShapesPopup.IsOpen = !BrushesShapesPopup.IsOpen;
    }

    private void TextNotesGroup_Click(object sender, RoutedEventArgs e)
    {
        CursorPopup.IsOpen = false; LinePopup.IsOpen = false; FibPopup.IsOpen = false; PatternsPopup.IsOpen = false; BrushesShapesPopup.IsOpen = false; ForecastPopup.IsOpen = false; IconStampPopup.IsOpen = false;
        TextNotesPopup.IsOpen = !TextNotesPopup.IsOpen;
    }

    private void IconStampGroup_Click(object sender, RoutedEventArgs e)
    {
        CursorPopup.IsOpen = false; LinePopup.IsOpen = false; FibPopup.IsOpen = false;
        PatternsPopup.IsOpen = false; BrushesShapesPopup.IsOpen = false;
        TextNotesPopup.IsOpen = false; ForecastPopup.IsOpen = false;
        IconStampPopup.IsOpen = !IconStampPopup.IsOpen;
    }
    private async void IconStampItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;
        string label;
        int fontSize;
        string fontFamily;
        if (tag.StartsWith("e:"))
        {
            label = tag.Substring(2);
            fontSize = 22;
            fontFamily = "Segoe UI Emoji";
        }
        else if (tag.StartsWith("i:"))
        {
            label = tag.Substring(2);
            fontSize = 22;
            fontFamily = "Segoe MDL2 Assets";
        }
        else if (tag.StartsWith("s:"))
        {
            label = tag.Substring(2);
            fontSize = 36;
            fontFamily = "Segoe UI Emoji";
        }
        else return;
        _pendingStickerLabel = label;
        _pendingStickerFontSize = fontSize;
        _pendingStickerFontFamily = fontFamily;
        IconStampPopup.IsOpen = false;
        SetActiveTool(IconStampGroupButton);
        _activeDrawingMode = "sticker";
        if (_activeCursorMode != "cross")
        {
            _activeCursorMode = "cross";
            UpdateCursorButtonIcon();
        }
        await SendDrawingModeToChartAsync("sticker");
    }
    private void ForecastGroup_Click(object sender, RoutedEventArgs e)
    {
        CursorPopup.IsOpen = false; LinePopup.IsOpen = false; FibPopup.IsOpen = false; PatternsPopup.IsOpen = false; BrushesShapesPopup.IsOpen = false; TextNotesPopup.IsOpen = false; IconStampPopup.IsOpen = false;
    }
    private async void MeasureButton_Click(object sender, RoutedEventArgs e)
    {
        CursorPopup.IsOpen = false; LinePopup.IsOpen = false; FibPopup.IsOpen = false; PatternsPopup.IsOpen = false; BrushesShapesPopup.IsOpen = false; TextNotesPopup.IsOpen = false; ForecastPopup.IsOpen = false; IconStampPopup.IsOpen = false;
        SetActiveTool(MeasureButton);
        _activeDrawingMode = "measure";
        if (_activeCursorMode != "cross")
        {
            _activeCursorMode = "cross";
            UpdateCursorButtonIcon();
        }
        await SendDrawingModeToChartAsync("measure");
    }
    private async void ZoomButton_Click(object sender, RoutedEventArgs e)
    {
        CursorPopup.IsOpen = false; LinePopup.IsOpen = false; FibPopup.IsOpen = false; PatternsPopup.IsOpen = false; BrushesShapesPopup.IsOpen = false; TextNotesPopup.IsOpen = false; ForecastPopup.IsOpen = false; IconStampPopup.IsOpen = false;
        SetActiveTool(ZoomButton);
        _activeDrawingMode = "zoom";
        if (_activeCursorMode != "cross")
        {
            _activeCursorMode = "cross";
            UpdateCursorButtonIcon();
        }
        await SendDrawingModeToChartAsync("zoom");
    }
    private void LockAllDrawingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = SettingsStorageService.Load();
        settings.LockAllDrawingsEnabled = !settings.LockAllDrawingsEnabled;
        SettingsStorageService.Save(settings);
        UpdateLockAllDrawingsButtonVisual();
        _ = SendLockAllToChartAsync();
    }
    private void UpdateLockAllDrawingsButtonVisual()
    {
        var settings = SettingsStorageService.Load();
        LockAllDrawingsButton.Background = settings.LockAllDrawingsEnabled ? (Brush)FindResource("Accent") : Brushes.Transparent;
        LockAllIcon.Content = FindResource(settings.LockAllDrawingsEnabled ? "Icon_lockall_active" : "Icon_lockall");
    }
    private async Task SendLockAllToChartAsync()
    {
        try { await _chartPageReady.Task; } catch { return; }
        if (ChartWebView.CoreWebView2 is null) return;
        ChartWebView.CoreWebView2.PostWebMessageAsJson(
        JsonSerializer.Serialize(new { type = "setLockAllDrawings", enabled = SettingsStorageService.Load().LockAllDrawingsEnabled }));
    }
    private async void MagnetButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = SettingsStorageService.Load();
        settings.MagnetEnabled = !settings.MagnetEnabled;
        SettingsStorageService.Save(settings);
        UpdateMagnetButtonVisual();
        await SendMagnetToChartAsync();
    }
    private void MagnetButton_RightClick(object sender, MouseButtonEventArgs e)
    {
        UpdateMagnetButtonVisual();
        MagnetPopup.IsOpen = true;
    }
    private async void MagnetModeItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string mode) return;
        var settings = SettingsStorageService.Load();
        settings.MagnetMode = mode == "strong" ? "strong" : "weak";
        settings.MagnetEnabled = true;
        SettingsStorageService.Save(settings);
        MagnetPopup.IsOpen = false;
        UpdateMagnetButtonVisual();
        await SendMagnetToChartAsync();
    }
    private void UpdateMagnetButtonVisual()
    {
        var settings = SettingsStorageService.Load();
        MagnetButton.Background = settings.MagnetEnabled ? (Brush)FindResource("Accent") : Brushes.Transparent;
        MagnetIcon.Content = FindResource(settings.MagnetEnabled ? "Icon_magnet_active" : "Icon_magnet");
        MagnetWeakItem.Foreground = settings.MagnetMode == "weak" ? (Brush)FindResource("Accent") : (Brush)FindResource("TextSecondary");
        MagnetStrongItem.Foreground = settings.MagnetMode == "strong" ? (Brush)FindResource("Accent") : (Brush)FindResource("TextSecondary");
    }
    private async Task SendMagnetToChartAsync()
    {
        try { await _chartPageReady.Task; } catch { return; }
        if (ChartWebView.CoreWebView2 is null) return;
        var settings = SettingsStorageService.Load();
        ChartWebView.CoreWebView2.PostWebMessageAsJson(
        JsonSerializer.Serialize(new { type = "setMagnet", enabled = settings.MagnetEnabled, mode = settings.MagnetMode }));
    }
    private async void ToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;
        var symbolClean = _chartSymbol.Replace("/", "");

        if (tag == "clear")
        {
            var res = MessageBox.Show($"Delete all drawings for {_chartSymbol}?", "Meowgnal", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;
            CaptureSnapshot();
            _drawingsFile.Drawings.RemoveAll(d => d.Symbol == symbolClean);
            DrawingStorageService.Save(_drawingsFile);
            await SendDrawingsToChartAsync();
            RebuildObjectList();
            return;
        }

        if (tag == "auto_sr")
        {
            if (_currentBars.Count == 0) return;
            var autoLevels = SupportResistanceDetector.Detect(_chartSymbol, _currentBars);
            foreach (var d in autoLevels) d.DataSource = _chartDataSource;
            CaptureSnapshot(); _drawingsFile.Drawings.AddRange(autoLevels);
            DrawingStorageService.Save(_drawingsFile);
            await SendDrawingsToChartAsync();
            RebuildObjectList();
            NotificationService.ShowToast("Meowgnal", $"Detected {autoLevels.Count} important S/R levels.");
            return;
        }

        CursorPopup.IsOpen = false;
        LinePopup.IsOpen = false;
        FibPopup.IsOpen = false;
        PatternsPopup.IsOpen = false;
        BrushesShapesPopup.IsOpen = false;
        TextNotesPopup.IsOpen = false;
        ForecastPopup.IsOpen = false;

        var cursorMap = new Dictionary<string, string>
        {
            ["cur_cross"] = "cross",
            ["cur_dot"] = "dot",
            ["cur_arrow"] = "arrow",
            ["cur_demo"] = "demonstration",
            ["cur_magic"] = "magic",
            ["cur_eraser"] = "eraser",
        };
        if (cursorMap.TryGetValue(tag, out var cursorModeName))
        {
            _activeCursorMode = cursorModeName;
            UpdateCursorButtonIcon();
            RefreshCursorMenuHighlight();
            SetActiveTool(null);
            await SendCursorModeToChartAsync(cursorModeName);
            return;
        }

        var group = tag switch
        {
            "fib" or "fibextension" or "fibchannel" or "fibtimezone" or "trendbasedfibtime" or "fibcircles" or "fibspiral"
                or "fibarcs" or "fibwedge" or "fibspeedfan" or "pitchfan"
                or "gannbox" or "gannsquare" or "gannsquarefixed" or "gannfan" => FibGroupButton,

            "xabcdpattern" or "cypherpattern" or "headandshoulders" or "abcdpattern" or "trianglepattern" or "threedrivespattern"
                or "elliottimpulsewave" or "elliottcorrectionwave" or "elliotttrianglewave" or "elliottdoublecombowave" or "elliotttriplecombowave"
                or "cycliclines" or "timecycles" or "sineline" => PatternsGroupButton,

            "longposition" or "shortposition" or "positionforecast" or "barspattern" or "ghostfeed" or "sector"
or "anchoredvwap" or "fixedrangevolumeprofile" or "anchoredvolumeprofile"
or "pricerange" or "daterange" or "dateandpricerange" => ForecastGroupButton,

            "rectangle" or "rotatedrectangle" or "circle" or "ellipse" or "triangle" or "polyline" or "arc" or "path" or "curve" or "doublecurve"
or "arrow" or "arrowmarkup" or "arrowmarkdown" or "arrowmarker" or "brush" or "highlighter" => BrushesShapesGroupButton,

            "text" or "anchoredtext" or "note" or "anchorednote" or "callout" or "comment"
or "pricelabel" or "pricenote" or "signpost" or "flagmark" or "pin" or "table" => TextNotesGroupButton,

            "cur_cross" or "cur_dot" or "cur_arrow" or "cur_demo" or "cur_magic" or "cur_eraser" => CursorGroupButton,
            _ => LineGroupButton,
        };

        var mode = tag == "cursor" ? "none" : tag;
        SetActiveTool(tag == "cursor" ? null : group);
        if (_activeCursorMode != "cross")
        {
            _activeCursorMode = "cross";
            UpdateCursorButtonIcon();
        }
        await SendDrawingModeToChartAsync(mode);
    }

    private void KeepDrawingButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = SettingsStorageService.Load();
        settings.KeepDrawingEnabled = !settings.KeepDrawingEnabled;
        SettingsStorageService.Save(settings);
        UpdateKeepDrawingButtonVisual();
    }
    private void UpdateKeepDrawingButtonVisual()
    {
        var settings = SettingsStorageService.Load();
        KeepDrawingButton.Background = settings.KeepDrawingEnabled ? (Brush)FindResource("Accent") : Brushes.Transparent;
        KeepDrawingIcon.Content = FindResource(settings.KeepDrawingEnabled ? "Icon_keepdrawing_active" : "Icon_keepdrawing");
    }
    private void SetActiveTool(Button? active)
    {
        var railButtons = new[] { CursorGroupButton, LineGroupButton, FibGroupButton, PatternsGroupButton, ForecastGroupButton, MeasureButton, ZoomButton, BrushesShapesGroupButton, TextNotesGroupButton, IconStampGroupButton };
        foreach (var b in railButtons)
            b.Background = Brushes.Transparent;
        (active ?? CursorGroupButton).Background = (Brush)FindResource("Accent");
    }

    private async Task SendDrawingModeToChartAsync(string mode)
    {
        try { await _chartPageReady.Task; } catch { return; }
        if (ChartWebView.CoreWebView2 is null) return;
        ChartWebView.CoreWebView2.PostWebMessageAsJson(
            JsonSerializer.Serialize(new { type = "setDrawingMode", mode, color = _drawingsFile.DefaultColor }));
    }

    private async Task SendCursorModeToChartAsync(string mode)
    {
        try { await _chartPageReady.Task; } catch { return; }
        if (ChartWebView.CoreWebView2 is null) return;
        ChartWebView.CoreWebView2.PostWebMessageAsJson(
            JsonSerializer.Serialize(new { type = "setCursorMode", mode }));
    }

    private async Task SendLongPressTooltipAsync(bool enabled)
    {
        try { await _chartPageReady.Task; } catch { return; }
        if (ChartWebView.CoreWebView2 is null) return;
        ChartWebView.CoreWebView2.PostWebMessageAsJson(
            JsonSerializer.Serialize(new { type = "setLongPressTooltip", enabled }));
    }

    private async void LongPressTooltip_Click(object sender, RoutedEventArgs e)
    {
        var on = LongPressTooltipToggle.IsChecked == true;
        var settings = SettingsStorageService.Load();
        settings.LongPressTooltipEnabled = on;
        SettingsStorageService.Save(settings);
        await SendLongPressTooltipAsync(on);
    }

    private void UpdateCursorButtonIcon()
    {
        var key = _activeCursorMode switch
        {
            "dot" => "Icon_dot",
            "arrow" => "Icon_cursor",
            "demonstration" => "Icon_demo",
            "magic" => "Icon_magic",
            "eraser" => "Icon_eraser",
            _ => "Icon_cross",
        };
        CursorGroupIcon.Content = FindResource(key);
    }

    private void RefreshCursorMenuHighlight()
    {
        var items = new (Button Btn, string Mode)[]
        {
            (CursorCrossItem, "cross"),
            (CursorDotItem, "dot"),
            (CursorArrowItem, "arrow"),
            (CursorDemoItem, "demonstration"),
            (CursorMagicItem, "magic"),
            (CursorEraserItem, "eraser"),
        };
        foreach (var (btn, mode) in items)
        {
            var active = mode == _activeCursorMode;
            btn.Background = active ? (Brush)FindResource("BgPanel") : Brushes.Transparent;
            btn.Foreground = active ? (Brush)FindResource("Accent") : (Brush)FindResource("TextSecondary");
        }
    }

    private async Task SendDrawingsToChartAsync()
    {
        try { await _chartPageReady.Task; } catch { return; }
        if (ChartWebView.CoreWebView2 is null) return;
        var symbolClean = _chartSymbol.Replace("/", "");

        if (SettingsStorageService.Load().DrawingsHidden)
        {
            ChartWebView.CoreWebView2.PostWebMessageAsJson(
                JsonSerializer.Serialize(new { type = "setDrawings", drawings = Array.Empty<object>() }));
            return;
        }

        var drawings = _drawingsFile.Drawings
            .Where(d => d.Symbol == symbolClean && d.IsVisible)
            .Select(d => new
            {
                id = d.Id,
                kind = d.Kind.ToString().ToLowerInvariant(),
                color = d.Color,
                label = d.Label,
                alert = d.AlertOnCross,
                locked = d.IsLocked,
                lineWidth = d.LineWidth,
                lineStyle = d.LineStyle,
                groupId = d.GroupId,
                zIndex = d.ZIndex,
                fontSize = d.FontSize,
                fontFamily = d.FontFamily,
                gannRatios = d.GannRatios,
                extendLeft = d.ExtendLeft,
                extendRight = d.ExtendRight,
                showPriceLabels = d.ShowPriceLabels,
                showTimeLabel = d.ShowTimeLabel,
                showPriceChange = d.ShowPriceChange,
                showBarCount = d.ShowBarCount,
                showTimeElapsed = d.ShowTimeElapsed,
                showAngle = d.ShowAngle,
                fillBackground = d.FillBackground,
                fillOpacity = d.FillOpacity,
                showMedianLine = d.ShowMedianLine,
                medianLineColor = d.MedianLineColor,
                medianLineStyle = d.MedianLineStyle,
                stdDevMultiplier = d.StdDevMultiplier,
                secondLineColor = d.SecondLineColor,
                pitchforkUseSameColor = d.PitchforkUseSameColor,
                pitchforkMedianColor = d.PitchforkMedianColor,
                pitchforkArm1Color = d.PitchforkArm1Color,
                pitchforkArm2Color = d.PitchforkArm2Color,
                showRatios = d.ShowRatios,
                necklineColor = d.NecklineColor,
                showLabels = d.ShowLabels,
                showApex = d.ShowApex,
                labelColor = d.LabelColor,
                positionSide = d.PositionSide,
                entryPrice = (double)d.EntryPrice,
                stopLossPrice = (double)d.StopLossPrice,
                takeProfitPrice = (double)d.TakeProfitPrice,
                positionSizePercent = (double)d.PositionSizePercent,
                profitZoneColor = d.ProfitZoneColor,
                lossZoneColor = d.LossZoneColor,
                ghostSymbol = d.GhostSymbol,
                ghostDataSource = d.GhostDataSource,
                ghostOpacity = d.GhostOpacity,
                ghostCandles = d.GhostCandles?.Select(c => new { time = c.Timestamp, open = (double)c.Open, high = (double)c.High, low = (double)c.Low, close = (double)c.Close }).ToArray(),
                barsPatternOpacity = d.BarsPatternOpacity,
                sectorFillOpacity = d.SectorFillOpacity,
                showVwapBands = d.ShowVwapBands,
                volumeBucketCount = d.VolumeBucketCount,
                volumeProfileWidthPercent = d.VolumeProfileWidthPercent,
                volumeProfileColor = d.VolumeProfileColor,
                priceRangeMode = d.PriceRangeMode,
                dateRangeUnit = d.DateRangeUnit,
                cycleCount = d.CycleCount,
                cycleIntervalSeconds = d.CycleIntervalSeconds,
                sineAmplitudePercent = d.SineAmplitudePercent,
                sineRepeatCount = d.SineRepeatCount,
                arrowHeadStyle = d.ArrowHeadStyle,
                arrowMarkerDirection = d.ArrowMarkerDirection,
                fibLevels = d.FibLevels?.Select(l => new { ratio = l.Ratio, enabled = l.Enabled, color = l.Color, label = l.Label }).ToArray(),
                isBold = d.IsBold,
                isItalic = d.IsItalic,
                textBgColor = d.TextBgColor,
                textBgOpacity = d.TextBgOpacity,
                textBgEnabled = d.TextBgEnabled,
                textBorderColor = d.TextBorderColor,
                textBorderEnabled = d.TextBorderEnabled,
                anchoredPixelX = d.AnchoredPixelX,
                anchoredPixelY = d.AnchoredPixelY,
                tableRows = d.TableRows,
                tableCols = d.TableCols,
                tableBgColor = d.TableBgColor,
                tableBorderColor = d.TableBorderColor,
                tableData = d.TableData,
                points = d.Points.Select(p => new { time = p.TimeUnix, price = p.Price }).ToArray()
            }).ToArray();

        ChartWebView.CoreWebView2.PostWebMessageAsJson(
            JsonSerializer.Serialize(new { type = "setDrawings", drawings }));
    }

    #region Object Tree

    private void ObjectsButton_Click(object sender, RoutedEventArgs e)
    {
        RebuildObjectList();
        ObjectsSymbolText.Text = _chartSymbol;
        ObjectsPopup.IsOpen = !ObjectsPopup.IsOpen;
    }

    private string KindLabel(DrawingKind k) => k switch
    {
        DrawingKind.HorizontalLine => "Horizontal Line",
        DrawingKind.TrendLine => "Trend Line",
        DrawingKind.Fibonacci => "Fibonacci",
        DrawingKind.Ray => "Ray",
        DrawingKind.ExtendedLine => "Extended Line",
        DrawingKind.HorizontalRay => "Horizontal Ray",
        DrawingKind.VerticalLine => "Vertical Line",
        DrawingKind.Crossline => "Cross Line",
        DrawingKind.InfoLine => "Info Line",
        DrawingKind.TrendAngle => "Trend Angle",
        DrawingKind.ParallelChannel => "Parallel Channel",
        DrawingKind.RegressionTrend => "Regression Trend",
        DrawingKind.FlatTopBottom => "Flat Top/Bottom",
        DrawingKind.DisjointChannel => "Disjoint Channel",
        DrawingKind.Pitchfork => "Pitchfork",
        DrawingKind.SchiffPitchfork => "Schiff Pitchfork",
        DrawingKind.ModifiedSchiffPitchfork => "Modified Schiff",
        DrawingKind.InsidePitchfork => "Inside Pitchfork",
        DrawingKind.FibExtension => "Fib Extension",
        DrawingKind.FibChannel => "Fib Channel",
        DrawingKind.FibTimeZone => "Fib Time Zone",
        DrawingKind.TrendBasedFibTime => "Trend-based Fib Time",
        DrawingKind.FibCircles => "Fib Circles",
        DrawingKind.FibSpiral => "Fib Spiral",
        DrawingKind.FibArcs => "Fib Arcs",
        DrawingKind.FibWedge => "Fib Wedge",
        DrawingKind.FibSpeedFan => "Fib Speed Fan",
        DrawingKind.Pitchfan => "Pitchfan",
        DrawingKind.GannBox => "Gann Box",
        DrawingKind.GannSquare => "Gann Square",
        DrawingKind.GannSquareFixed => "Gann Square Fixed",
        DrawingKind.GannFan => "Gann Fan",
        DrawingKind.XabcdPattern => "XABCD Pattern",
        DrawingKind.CypherPattern => "Cypher Pattern",
        DrawingKind.HeadAndShoulders => "Head and Shoulders",
        DrawingKind.AbcdPattern => "ABCD Pattern",
        DrawingKind.TrianglePattern => "Triangle Pattern",
        DrawingKind.ThreeDrivesPattern => "Three Drives Pattern",
        DrawingKind.ElliottImpulseWave => "Elliott Impulse Wave",
        DrawingKind.ElliottCorrectionWave => "Elliott Correction Wave",
        DrawingKind.ElliottTriangleWave => "Elliott Triangle Wave",
        DrawingKind.ElliottDoubleComboWave => "Elliott Double Combo Wave",
        DrawingKind.ElliottTripleComboWave => "Elliott Triple Combo Wave",
        DrawingKind.CyclicLines => "Cyclic Lines",
        DrawingKind.TimeCycles => "Time Cycles",
        DrawingKind.SineLine => "Sine Line",
        DrawingKind.LongPosition => "Long Position",
        DrawingKind.ShortPosition => "Short Position",
        DrawingKind.PositionForecast => "Position Forecast",
        DrawingKind.BarsPattern => "Bars Pattern",
        DrawingKind.GhostFeed => "Ghost Feed",
        DrawingKind.Sector => "Sector",
        DrawingKind.AnchoredVwap => "Anchored VWAP",
        DrawingKind.FixedRangeVolumeProfile => "Fixed Range Volume Profile",
        DrawingKind.AnchoredVolumeProfile => "Anchored Volume Profile",
        DrawingKind.PriceRange => "Price Range",
        DrawingKind.DateRange => "Date Range",
        DrawingKind.DateAndPriceRange => "Date and Price Range",
        DrawingKind.Rectangle => "Rectangle",
        DrawingKind.RotatedRectangle => "Rotated Rectangle",
        DrawingKind.Circle => "Circle",
        DrawingKind.Ellipse => "Ellipse",
        DrawingKind.Triangle => "Triangle",
        DrawingKind.Polyline => "Polyline",
        DrawingKind.Arc => "Arc",
        DrawingKind.Arrow => "Arrow",
        DrawingKind.ArrowMarker => "Arrow Marker",
        DrawingKind.ArrowMarkUp => "Arrow Mark Up",
        DrawingKind.ArrowMarkDown => "Arrow Mark Down",
        DrawingKind.Path => "Path",
        DrawingKind.Curve => "Curve",
        DrawingKind.DoubleCurve => "Double Curve",
        DrawingKind.Brush => "Brush",
        DrawingKind.Highlighter => "Highlighter",
        DrawingKind.Text => "Text",
        DrawingKind.AnchoredText => "Anchored Text",
        DrawingKind.Note => "Note",
        DrawingKind.AnchoredNote => "Anchored Note",
        DrawingKind.Callout => "Callout",
        DrawingKind.Comment => "Comment",
        DrawingKind.PriceLabel => "Price Label",
        DrawingKind.PriceNote => "Price Note",
        DrawingKind.Signpost => "Signpost",
        DrawingKind.FlagMark => "Flag Mark",
        DrawingKind.Pin => "Pin",
        DrawingKind.Flag => "Flag",
        DrawingKind.Sticker => "Sticker",
        DrawingKind.Table => "Table",
        _ => k.ToString()
    };

    private void RebuildObjectList()
    {
        ObjectsListPanel.Children.Clear();
        var symbolClean = _chartSymbol.Replace("/", "");
        var items = _drawingsFile.Drawings.Where(d => d.Symbol == symbolClean).ToList();
        if (items.Count == 0)
        {
            ObjectsListPanel.Children.Add(new TextBlock
            {
                Text = "No drawings yet. Use the tools on the left rail.",
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 11,
                Margin = new Thickness(0, 8, 0, 8)
            });
            return;
        }
        foreach (var d in items)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = string.IsNullOrWhiteSpace(d.Label) ? KindLabel(d.Kind) : $"{KindLabel(d.Kind)} — {d.Label}";

            var leftSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Border dot;
            try
            {
                dot = new Border
                {
                    Width = 12,
                    Height = 12,
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 0, 8, 0),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(d.Color)),
                    BorderBrush = (Brush)FindResource("BorderLine"),
                    BorderThickness = new Thickness(1),
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            catch
            {
                dot = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 8, 0), Background = new SolidColorBrush(Colors.Gray), VerticalAlignment = VerticalAlignment.Center };
            }
            leftSp.Children.Add(dot);

            var nameText = new TextBlock
            {
                Text = label,
                Foreground = d.IsVisible ? (Brush)FindResource("TextPrimary") : (Brush)FindResource("TextMuted"),
                FontSize = 12,
                FontStyle = d.IsVisible ? FontStyles.Normal : FontStyles.Italic,
                VerticalAlignment = VerticalAlignment.Center
            };
            leftSp.Children.Add(nameText);

            var rightSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var editBtn = new Button
            {
                Content = "✏️",
                Style = (Style)FindResource("TvButton"),
                ToolTip = "Edit properties (label, color, alert)",
                Tag = d,
                Padding = new Thickness(4, 2, 4, 2),
                FontSize = 11
            };
            editBtn.Click += ObjectEdit_Click;

            var lockBtn = new Button
            {
                Content = d.IsLocked ? "🔒" : "🔓",
                Style = (Style)FindResource("TvButton"),
                ToolTip = d.IsLocked ? "Unlock (eraser can delete)" : "Lock (eraser cannot delete)",
                Tag = d,
                Padding = new Thickness(4, 2, 4, 2),
                FontSize = 11
            };
            lockBtn.Click += ObjectLock_Click;

            var hideBtn = new Button
            {
                Content = d.IsVisible ? "👁️" : "🚫",
                Style = (Style)FindResource("TvButton"),
                ToolTip = d.IsVisible ? "Hide drawing" : "Show drawing",
                Tag = d,
                Padding = new Thickness(4, 2, 4, 2),
                FontSize = 11
            };
            hideBtn.Click += ObjectHide_Click;

            var delBtn = new Button
            {
                Content = "🗑️",
                Style = (Style)FindResource("TvButton"),
                ToolTip = "Delete drawing",
                Tag = d,
                Padding = new Thickness(4, 2, 4, 2),
                FontSize = 11,
                Foreground = (Brush)FindResource("Down")
            };
            delBtn.Click += ObjectDelete_Click;

            rightSp.Children.Add(editBtn);
            rightSp.Children.Add(lockBtn);
            rightSp.Children.Add(hideBtn);
            rightSp.Children.Add(delBtn);

            Grid.SetColumn(leftSp, 0);
            Grid.SetColumn(rightSp, 1);
            row.Children.Add(leftSp);
            row.Children.Add(rightSp);
            ObjectsListPanel.Children.Add(row);
        }
    }

    private void ObjectLock_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Drawing d) return;
        d.IsLocked = !d.IsLocked;
        DrawingStorageService.Save(_drawingsFile);
        _ = SendDrawingsToChartAsync();
        RebuildObjectList();
    }

    private void ObjectHide_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Drawing d) return;
        d.IsVisible = !d.IsVisible;
        DrawingStorageService.Save(_drawingsFile);
        _ = SendDrawingsToChartAsync();
        RebuildObjectList();
    }

    private void ObjectDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Drawing d) return;
        if (d.IsLocked)
        {
            NotificationService.ShowToast("Meowgnal", "This drawing is locked. Unlock it first.");
            return;
        }
        _drawingsFile.Drawings.RemoveAll(x => x.Id == d.Id);
        DrawingStorageService.Save(_drawingsFile);
        _ = SendDrawingsToChartAsync();
        RebuildObjectList();
    }

    private async void ObjectEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Drawing d) return;
        var win = new DrawingPropertiesWindow(d) { Owner = this };
        if (win.ShowDialog() == true)
        {
            DrawingStorageService.Save(_drawingsFile);
            try { await SendDrawingsToChartAsync(); }
            catch (Exception ex) { AppLogger.Fatal("Error sending drawings to chart", ex); }
            RebuildObjectList();
        }
    }

    private void ExportTemplate_Click(object sender, RoutedEventArgs e)
    {
        var symbolClean = _chartSymbol.Replace("/", "");
        var drawings = _drawingsFile.Drawings.Where(d => d.Symbol == symbolClean).ToList();
        if (drawings.Count == 0)
        {
            NotificationService.ShowToast("Meowgnal", "No drawings to export.");
            return;
        }
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export drawing template",
            Filter = "Drawing template (*.meowtmpl.json)|*.meowtmpl.json",
            FileName = $"template_{symbolClean}_{DateTime.Now:yyyyMMdd_HHmmss}.meowtmpl.json"
        };
        if (dialog.ShowDialog() != true) return;
        var template = new DrawingTemplate
        {
            Name = Path.GetFileNameWithoutExtension(dialog.FileName),
            SourceSymbol = _chartSymbol,
            Drawings = drawings
        };
        if (TemplateService.Export(template, dialog.FileName))
            NotificationService.ShowToast("Meowgnal", $"Exported {drawings.Count} drawings.");
        else
            NotificationService.ShowToast("Meowgnal", "Export failed — see app.log.");
    }

    private void ImportTemplate_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import drawing template",
            Filter = "Drawing template (*.meowtmpl.json)|*.meowtmpl.json"
        };
        if (dialog.ShowDialog() != true) return;
        var template = TemplateService.Import(dialog.FileName);
        if (template is null || template.Drawings.Count == 0)
        {
            NotificationService.ShowToast("Meowgnal", "Could not read template.");
            return;
        }
        var symbolClean = _chartSymbol.Replace("/", "");
        foreach (var d in template.Drawings)
        {
            d.Symbol = symbolClean;
            d.DataSource = _chartDataSource;
            d.Id = Guid.NewGuid().ToString("N");
            d.IsAutoDetected = false;
            _drawingsFile.Drawings.Add(d);
        }
        DrawingStorageService.Save(_drawingsFile);
        _ = SendDrawingsToChartAsync();
        RebuildObjectList();
        NotificationService.ShowToast("Meowgnal", $"Imported {template.Drawings.Count} drawings from {template.Name}.");
    }

    #endregion

    #region Indicator Panel

    private readonly IndicatorSettingsFile _indicatorSettings = IndicatorSettingsStorageService.Load();
    private static readonly System.Text.Json.JsonSerializerOptions _indicatorJsonOptions = new()
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    private void IndicatorButton_Click(object sender, RoutedEventArgs e)
    {
        IndicatorPanelControl.RefreshActiveTypes(GetActiveIndicators(_chartSymbol).Select(a => a.Type));
        IndicatorPopup.IsOpen = !IndicatorPopup.IsOpen;
    }

    private List<ActiveIndicator> GetActiveIndicators(string symbol)
    {
        if (!_indicatorSettings.ActiveIndicators.TryGetValue(symbol, out var list))
        {
            list = new List<ActiveIndicator>();
            _indicatorSettings.ActiveIndicators[symbol] = list;
        }
        return list;
    }

    private object? BuildIndicatorPayload(string type, int period, string? customColor, int lineWidth)
    {
        var bars = _currentBars;
        if (bars.Count == 0) return null;
        var tk = type.ToLowerInvariant();
        var displayMode = IsOverlayIndicator(tk) ? "Overlay" : "Pane";
        var color = string.IsNullOrWhiteSpace(customColor) ? DefaultIndicatorColor(tk) : customColor;
        if (lineWidth < 1 || lineWidth > 4) lineWidth = 2;
        GetPaneScale(tk, out var range, out var guides);

        var series = new List<object>();
        if (tk == "macd")
        {
            var m = IndicatorCalculator.MACD(bars);
            series.Add(new { key = tk, name = "MACD", values = m.MACD.ToArray(), color, lineStyle = "" });
            series.Add(new { key = tk + ".signal", name = "Signal", values = m.Signal.ToArray(), color = "#FF9800", lineStyle = "" });
            series.Add(new { key = tk + ".hist", name = "Hist", values = m.Histogram.ToArray(), color = "#787B86", lineStyle = "", histogram = true });
        }
        else if (tk == "stoch")
        {
            var s = IndicatorCalculator.STOCH(bars);
            series.Add(new { key = tk, name = "%K", values = s.K.ToArray(), color, lineStyle = "" });
            series.Add(new { key = tk + ".d", name = "%D", values = s.D.ToArray(), color = "#FF9800", lineStyle = "" });
        }
        else
        {
            var def = new IndicatorDefinition
            {
                Id = tk,
                Type = type,
                DisplayMode = displayMode,
                Params = new Dictionary<string, double> { ["period"] = period }
            };
            Dictionary<string, double?[]> multi;
            try { multi = IndicatorEngine.CalculateMulti(bars, def); }
            catch { return null; }
            foreach (var kv in multi.OrderBy(k => k.Key))
            {
                var sub = kv.Key.Length > tk.Length ? kv.Key.Substring(tk.Length + 1) : "";
                series.Add(new
                {
                    key = kv.Key,
                    name = string.IsNullOrEmpty(sub) ? type.ToUpperInvariant() : sub,
                    values = kv.Value,
                    color = SubColor(tk, sub, color),
                    lineStyle = SubStyle(tk, sub) ?? ""
                });
            }
        }

        return new
        {
            type = "addIndicator",
            id = tk,
            kind = tk,
            displayMode,
            label = IndicatorLabel(tk, period),
            period,
            color,
            lineWidth,
            fixedRange = range,
            guideLines = guides,
            series
        };
    }

    private static string IndicatorLabel(string tk, int period) => tk switch
    {
        "macd" => "MACD 12·26·9",
        "sar" => "Parabolic SAR",
        "vwap" => "VWAP",
        "feargreed" => "Fear & Greed",
        "btcdom" => "BTC Dominance",
        "funding" => "Funding Rate",
        "oi" => "Open Interest",
        _ => $"{tk.ToUpperInvariant()} {period}"
    };

    private static string DefaultIndicatorColor(string tk) => tk switch
    {
        "ema" => "#FF9800",
        "wma" => "#00BCD4",
        "hma" => "#9C27B0",
        "dema" => "#4CAF50",
        "tema" => "#795548",
        "kama" => "#E91E63",
        "vwma" => "#3F51B5",
        "vwap" => "#E91E63",
        "bbands" => "#2962FF",
        "keltner" => "#00BCD4",
        "donchian" => "#FF9800",
        "sar" => "#F23645",
        "supertrend" => "#089981",
        "rsi" => "#9C27B0",
        "stoch" => "#9C27B0",
        "stochrsi" => "#E91E63",
        "cci" => "#00BCD4",
        "williamsr" => "#FF9800",
        "mfi" => "#4CAF50",
        "roc" => "#3F51B5",
        "trix" => "#795548",
        "ultimate" => "#E91E63",
        "ao" => "#089981",
        "cmo" => "#00BCD4",
        "connorsrsi" => "#9C27B0",
        "macd" => "#2962FF",
        "adx" => "#FF5722",
        "atr" => "#00BCD4",
        "stddev" => "#3F51B5",
        "ulcer" => "#F23645",
        "volsma" => "#4CAF50",
        "obv" => "#4CAF50",
        "cmf" => "#089981",
        "forceindex" => "#FF9800",
        "adl" => "#00BCD4",
        "aroon" => "#089981",
        "vortex" => "#089981",
        "chop" => "#787B86",
        "feargreed" => "#FF9800",
        "btcdom" => "#F7A600",
        "funding" => "#9C27B0",
        "oi" => "#00BCD4",
        _ => "#2962FF"
    };

    private static string SubColor(string tk, string sub, string primary) => (tk, sub) switch
    {
        ("ichimoku", "kijun") => "#FF9800",
        ("ichimoku", "senkouA") => "#089981",
        ("ichimoku", "senkouB") => "#F23645",
        ("ichimoku", "chikou") => "#9C27B0",
        ("aroon", "down") => "#F23645",
        ("vortex", "minus") => "#F23645",
        ("stoch", "d") => "#FF9800",
        _ => primary
    };

    private static string? SubStyle(string tk, string sub) => (tk, sub) switch
    {
        ("bbands", "upper") or ("bbands", "lower") => "dotted",
        ("keltner", "upper") or ("keltner", "lower") => "dotted",
        ("donchian", "upper") or ("donchian", "lower") => "dotted",
        _ => null
    };

    private static void GetPaneScale(string tk, out double[]? range, out double[]? guides)
    {
        switch (tk)
        {
            case "rsi": range = new double[] { 0, 100 }; guides = new double[] { 30, 70 }; break;
            case "stoch":
            case "stochrsi":
            case "mfi": range = new double[] { 0, 100 }; guides = new double[] { 20, 80 }; break;
            case "ultimate": range = new double[] { 0, 100 }; guides = new double[] { 30, 70 }; break;
            case "connorsrsi": range = new double[] { 0, 100 }; guides = new double[] { 10, 90 }; break;
            case "williamsr": range = new double[] { -100, 0 }; guides = new double[] { -80, -20 }; break;
            case "adx": range = new double[] { 0, 100 }; guides = new double[] { 25 }; break;
            case "cmo": range = new double[] { -100, 100 }; guides = null; break;
            case "chop": range = new double[] { 0, 100 }; guides = new double[] { 38.2, 61.8 }; break;
            case "feargreed": range = new double[] { 0, 100 }; guides = new double[] { 25, 75 }; break;
            case "cmf":
            case "funding": range = null; guides = new double[] { 0 }; break;
            default: range = null; guides = null; break;
        }
    }

    private static bool IsOverlayIndicator(string type)
    {
        switch (type)
        {
            case "sma":
            case "ema":
            case "wma":
            case "hma":
            case "dema":
            case "tema":
            case "kama":
            case "vwma":
            case "vwap":
            case "bbands":
            case "keltner":
            case "donchian":
            case "sar":
            case "supertrend":
            case "ichimoku":
                return true;
            default:
                return false;
        }
    }

    private async Task RefreshIndicatorsOnChartAsync()
    {
        try
        {
            await _chartPageReady.Task;
            if (ChartWebView.CoreWebView2 is null || _currentBars.Count == 0) return;

            var activeDefs = GetActiveIndicators(_chartSymbol)
                .Select(a => new IndicatorDefinition
                {
                    Id = a.Type,
                    Type = a.Type,
                    Params = new Dictionary<string, double> { ["period"] = a.Period }
                })
                .ToList();
            await IndicatorEngine.PrefetchFundamentalsAsync(_currentBars, activeDefs, _chartDataSource, _chartSymbol);

            ChartWebView.CoreWebView2.PostWebMessageAsJson(
                System.Text.Json.JsonSerializer.Serialize(new { type = "clearIndicators" }, _indicatorJsonOptions));

            foreach (var a in GetActiveIndicators(_chartSymbol).ToList())
            {
                var payload = BuildIndicatorPayload(a.Type, a.Period, a.Color, a.LineWidth);
                if (payload is not null)
                    ChartWebView.CoreWebView2.PostWebMessageAsJson(
                        System.Text.Json.JsonSerializer.Serialize(payload, _indicatorJsonOptions));
            }
        }
        catch { }
    }

    private async void AddIndicatorToChart(IndicatorInfo info)
    {
        try
        {
            var typeKey = (info.Type ?? "").ToLowerInvariant();
            var list = GetActiveIndicators(_chartSymbol);
            var existing = list.FirstOrDefault(a => a.Type == typeKey);
            if (existing is not null) list.Remove(existing);
            else list.Add(new ActiveIndicator { Type = typeKey, Period = info.DefaultPeriod });
            IndicatorSettingsStorageService.Save(_indicatorSettings);
            IndicatorPanelControl.RefreshActiveTypes(list.Select(a => a.Type));
            await RefreshIndicatorsOnChartAsync();
            IndicatorPopup.IsOpen = false;
        }
        catch (Exception ex)
        {
            NotificationService.ShowToast("Meowgnal", $"Error adding indicator: {ex.Message}");
        }
    }

    #endregion

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

    private async void UndoRedo_KeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (!ctrl) return;
        if (Keyboard.FocusedElement is TextBox or PasswordBox) return;
        if (e.Key == Key.Z)
        {
            var restored = _undoManager.Undo(_drawingsFile.Drawings);
            if (restored is not null)
            {
                await RestoreSnapshotAsync(restored);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Y)
        {
            var restored = _undoManager.Redo(_drawingsFile.Drawings);
            if (restored is not null)
            {
                await RestoreSnapshotAsync(restored);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.C)
        {
            if (ChartWebView.CoreWebView2 is not null)
            {
                await ChartWebView.CoreWebView2.ExecuteScriptAsync("copySelectedDrawing();");
                e.Handled = true;
            }
        }
        else if (e.Key == Key.V)
        {
            if (ChartWebView.CoreWebView2 is not null)
            {
                await ChartWebView.CoreWebView2.ExecuteScriptAsync("pasteCopiedDrawing();");
                e.Handled = true;
            }
        }
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
            var settings = SettingsStorageService.Load();
            List<Bar>? htfBars = null;
            var htf = AccuracyService.NextHtf(strategy.Timeframe);
            if (htf is not null)
                htfBars = await provider.GetHistoricalCandlesAsync(strategy.Symbol, htf, limit: 120);

            await IndicatorEngine.PrefetchFundamentalsAsync(bars, strategy.Indicators, strategy.DataSource, strategy.Symbol);
            var signals = RuleEngine.ScanForSignals(strategy, bars);
            if (SettingsStorageService.Load().AccuracyClosedCandleOnly && bars.Count > 0)
                signals = signals.Where(s => s.Timestamp != bars[^1].Timestamp).ToList();

            var backtest = BacktestEngine.Run(strategy, bars, startingBalance: 10000m, feePercent: 0.1m, slippagePercent: 0.05m);
            totalWinRate += backtest.WinRatePercent;
            totalSignalCount += signals.Count;

            foreach (var s in signals)
            {
                _knownSignalKeys.Add(MakeSignalKey(strategy.StrategyId, s));
                var quality = AccuracyService.CalculateQuality(s, bars, htfBars, settings);
                allSignals.Add((new SignalDisplayItem
                {
                    Symbol = strategy.Symbol,
                    Description = strategy.Name,
                    Type = s.Type == SignalType.Entry ? "buy" : "sell",
                    Time = s.Timestamp.ToString("g"),
                    QualityScore = quality.Score,
                    QualityLabel = quality.Label,
                    QualityReason = quality.Reason
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
        await RefreshIndicatorsOnChartAsync();
        _ = SendPositionsToChartAsync();
        _ = SendDrawingsToChartAsync();
        _ = SendThemeToChartAsync();
        _ = SendCursorModeToChartAsync(_activeCursorMode);
        _ = SendLongPressTooltipAsync(SettingsStorageService.Load().LongPressTooltipEnabled);
        _ = SendLockAllToChartAsync();
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

    private void CaptureSnapshot()
    {
        _undoManager.PushSnapshot(_drawingsFile.Drawings);
    }

    private async Task RestoreSnapshotAsync(List<Drawing> restoredDrawings)
    {
        _drawingsFile.Drawings = restoredDrawings;
        DrawingStorageService.Save(_drawingsFile);
        await SendDrawingsToChartAsync();
        RebuildObjectList();
    }

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

    private async Task<bool> AccuracyPassAsync(FoundSignal f, AppSettings settings)
    {
        try
        {
            IDataProvider provider = f.Strategy.DataSource == "hyperliquid"
                ? new HyperliquidDataProvider()
                : new BinanceDataProvider();
            if (settings.AccuracyClosedCandleOnly)
            {
                var bars = await provider.GetHistoricalCandlesAsync(f.Strategy.Symbol, f.Strategy.Timeframe, limit: 5);
                if (AccuracyService.IsFormingCandleSignal(f.Signal, bars)) return false;
            }
            if (f.Signal.Type == SignalType.Entry)
            {
                if (settings.AccuracyMtfFilter)
                {
                    var htf = AccuracyService.NextHtf(f.Strategy.Timeframe);
                    if (htf is not null)
                    {
                        var htfBars = await provider.GetHistoricalCandlesAsync(f.Strategy.Symbol, htf, limit: 120);
                        if (!AccuracyService.HtfTrendOk(htfBars, f.Signal.Type)) return false;
                    }
                }
                if (settings.AccuracyVolumeFilter || settings.AccuracyRegimeFilter)
                {
                    var bars = await provider.GetHistoricalCandlesAsync(f.Strategy.Symbol, f.Strategy.Timeframe, limit: 150);
                    if (settings.AccuracyVolumeFilter && !AccuracyService.VolumeOk(bars, settings.AccuracyVolumeMultiplier)) return false;
                    if (settings.AccuracyRegimeFilter && !AccuracyService.RegimeOk(bars)) return false;
                }
            }
            return true;
        }
        catch
        {
            return true;
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

            var passed = new List<FoundSignal>();
            foreach (var f in fresh)
                if (await AccuracyPassAsync(f, settings)) passed.Add(f);
            fresh = passed;

            if (fresh.Count == 0) return;

            foreach (var f in fresh)
            {
                _knownSignalKeys.Add(MakeSignalKey(f.Strategy.StrategyId, f.Signal));

                IDataProvider provider = f.Strategy.DataSource == "hyperliquid"
                    ? new HyperliquidDataProvider()
                    : new BinanceDataProvider();
                var bars = await provider.GetHistoricalCandlesAsync(f.Strategy.Symbol, f.Strategy.Timeframe, limit: 500);
                var htf = AccuracyService.NextHtf(f.Strategy.Timeframe);
                List<Bar>? htfBars = null;
                if (htf is not null)
                    htfBars = await provider.GetHistoricalCandlesAsync(f.Strategy.Symbol, htf, limit: 120);
                var quality = AccuracyService.CalculateQuality(f.Signal, bars, htfBars, settings);

                _signals.Insert(0, new SignalDisplayItem
                {
                    Symbol = f.Strategy.Symbol,
                    Description = f.Strategy.Name,
                    Type = f.Signal.Type == SignalType.Entry ? "buy" : "sell",
                    Time = f.Signal.Timestamp.ToString("g"),
                    QualityScore = quality.Score,
                    QualityLabel = quality.Label,
                    QualityReason = quality.Reason
                });

                NotificationService.NotifySignal(
                    f.Strategy.Name,
                    f.Signal.Type == SignalType.Entry ? "Entry" : "Exit",
                    f.Strategy.Symbol,
                    f.Strategy.Timeframe,
                    bars[^1].Close);
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
                await IndicatorEngine.PrefetchFundamentalsAsync(bars, strategy.Indicators, strategy.DataSource, strategy.Symbol);
                foreach (var signal in RuleEngine.ScanForSignals(strategy, bars))
                    found.Add(new FoundSignal(strategy, signal));
            }
            catch
            {
            }
        }
        return found;
    }

    #region Replay Mode

    private async void ReplayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_replayMode)
        {
            await ExitReplayModeAsync();
            return;
        }
        await EnterReplayModeAsync();
    }

    private async Task EnterReplayModeAsync()
    {
        try
        {
            IDataProvider provider = _chartDataSource == "hyperliquid" ? new HyperliquidDataProvider() : new BinanceDataProvider();
            var bars = await provider.GetHistoricalCandlesAsync(_chartSymbol, _chartTimeframe, limit: 1000);
            if (bars.Count < 100)
            {
                NotificationService.ShowToast("Meowgnal", "Not enough history for replay on this symbol/timeframe.");
                return;
            }
            _replayBars = bars;
            _replayMode = true;
            ReplayBar.Visibility = Visibility.Visible;
            ReplayButton.Background = (Brush)FindResource("Accent");
            ApplyChartType("candles");
            _ = SendChartTypeAsync("candles");
            _ = SendClearIndicatorsAsync();
            ReplayDatePicker.DisplayDateStart = bars[0].Timestamp;
            ReplayDatePicker.DisplayDateEnd = bars[^1].Timestamp;
            ReplayDatePicker.SelectedDate = bars[(int)(bars.Count * 0.6)].Timestamp;
            UpdateReplayProgress();
            ResetReplaySession();
        }
        catch (Exception ex)
        {
            NotificationService.ShowToast("Meowgnal", $"Replay failed to load: {ex.Message}");
        }
    }

    private async Task ExitReplayModeAsync()
    {
        ForceExitReplayUi();
        await LoadChartAsync();
    }

    private void ForceExitReplayUi()
    {
        if (_guessTotal > 0)
            NotificationService.ShowToast("Meowgnal — Replay Summary",
                $"Session finished: {_guessCorrect}/{_guessTotal} correct guesses ({(double)_guessCorrect / _guessTotal * 100:N0}%).");
        _replayMode = false;
        StopReplayPlayback();
        ReplayBar.Visibility = Visibility.Collapsed;
        ReplayButton.Background = Brushes.Transparent;
        _replayBars = new List<Bar>();
        ResetReplaySession();
    }

    private void ReplayDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_replayMode) return;
        StopReplayPlayback();
        ResetReplayToDate();
    }

    private void ResetReplayToDate()
    {
        if (_replayBars.Count == 0) return;
        var start = ReplayDatePicker.SelectedDate ?? _replayBars[(int)(_replayBars.Count * 0.6)].Timestamp;
        var idx = _replayBars.FindIndex(b => b.Timestamp >= start);
        if (idx < 30) idx = Math.Min(30, _replayBars.Count);
        _replayShown = idx;
        _ = SendCandlesToChartAsync(_replayBars.Take(idx).ToList());
        UpdateReplayProgress();
    }

    private void ReplayReset_Click(object sender, RoutedEventArgs e)
    {
        if (!_replayMode) return;
        StopReplayPlayback();
        ResetReplayToDate();
    }

    private void ReplayStep_Click(object sender, RoutedEventArgs e) => ReplayStep(1);

    private void ReplayStep(int count)
    {
        if (!_replayMode || _replayBars.Count == 0) return;
        for (var i = 0; i < count && _replayShown < _replayBars.Count; i++)
        {
            _ = SendAppendCandleAsync(_replayBars[_replayShown]);
            _replayShown++;
        }
        UpdateReplayProgress();
        if (_replayShown >= _replayBars.Count) StopReplayPlayback();
    }

    private void ReplayPlay_Click(object sender, RoutedEventArgs e)
    {
        if (_replayTimer.IsEnabled) StopReplayPlayback();
        else StartReplayPlayback();
    }

    private void StartReplayPlayback()
    {
        if (!_replayMode || _replayShown >= _replayBars.Count) return;
        _replayTimer.Start();
        ReplayPlayButton.Content = "⏸ Pause";
    }

    private void StopReplayPlayback()
    {
        _replayTimer.Stop();
        ReplayPlayButton.Content = "▶ Play";
    }

    private void ReplaySpeedCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ReplaySpeedCombo.SelectedItem is ComboBoxItem ci && ci.Tag is string tag && int.TryParse(tag, out var ms))
            _replayTimer.Interval = TimeSpan.FromMilliseconds(ms);
    }

    private void UpdateReplayProgress()
    {
        ReplayProgressText.Text = $"Replay: {_replayShown}/{_replayBars.Count} candles";
    }

    private async Task SendAppendCandleAsync(Bar b)
    {
        try { await _chartPageReady.Task; } catch { return; }
        if (ChartWebView.CoreWebView2 is null) return;
        var payload = new
        {
            type = "appendCandle",
            candle = new
            {
                time = new DateTimeOffset(b.Timestamp).ToUnixTimeSeconds(),
                open = b.Open,
                high = b.High,
                low = b.Low,
                close = b.Close,
                volume = b.Volume
            }
        };
        ChartWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
    }

    private async Task SendClearIndicatorsAsync()
    {
        try { await _chartPageReady.Task; } catch { return; }
        if (ChartWebView.CoreWebView2 is null) return;
        ChartWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "clearIndicators" }));
    }

    private void BlindModeCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (BlindModeCheck is null) return;
        _ = SendBlindModeAsync(BlindModeCheck.IsChecked == true);
    }

    private async Task SendBlindModeAsync(bool blind)
    {
        try { await _chartPageReady.Task; } catch { return; }
        if (ChartWebView.CoreWebView2 is null) return;
        ChartWebView.CoreWebView2.PostWebMessageAsJson(
            JsonSerializer.Serialize(new { type = "setBlindMode", blind }));
    }

    private sealed class ReplayGuess
    {
        public string Choice { get; set; } = "";
        public int BarIndex { get; set; }
        public decimal EntryPrice { get; set; }
    }

    private ReplayGuess? _pendingGuess;
    private int _guessCorrect;
    private int _guessTotal;

    private void ReplayGuessButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_replayMode || _replayShown <= 0) return;
        GuessPopup.IsOpen = !GuessPopup.IsOpen;
    }

    private void GuessLong_Click(object sender, RoutedEventArgs e) => StartGuess("long");
    private void GuessShort_Click(object sender, RoutedEventArgs e) => StartGuess("short");
    private void GuessSkip_Click(object sender, RoutedEventArgs e) => StartGuess("skip");

    private void StartGuess(string choice)
    {
        GuessPopup.IsOpen = false;
        if (!_replayMode || _replayShown <= 0) return;
        StopReplayPlayback();
        _pendingGuess = new ReplayGuess
        {
            Choice = choice,
            BarIndex = _replayShown - 1,
            EntryPrice = _replayBars[_replayShown - 1].Close
        };
        ReplayStep(10);
        ResolvePendingGuess();
    }

    private void ResolvePendingGuess()
    {
        if (_pendingGuess is null || _replayBars.Count == 0) return;
        var guess = _pendingGuess;
        _pendingGuess = null;
        var exit = _replayBars[_replayShown - 1].Close;
        var longPct = guess.EntryPrice != 0 ? (exit - guess.EntryPrice) / guess.EntryPrice * 100m : 0m;
        var shortPct = -longPct;
        string resultText;
        if (guess.Choice == "skip")
        {
            _guessTotal++;
            resultText = $"⏭ Skipped — Long would be {longPct:+0.00;-0.00}%, Short {shortPct:+0.00;-0.00}%";
        }
        else
        {
            var pct = guess.Choice == "long" ? longPct : shortPct;
            var correct = pct > 0;
            _guessTotal++;
            if (correct) _guessCorrect++;
            resultText = $"{(guess.Choice == "long" ? "🟢 Long" : "🔴 Short")}: {pct:+0.00;-0.00}% — {(correct ? "correct ✅" : "wrong ❌")}";
        }
        UpdateSessionText();
        NotificationService.ShowToast("Meowgnal — Replay", resultText);
    }

    private void ResetReplaySession()
    {
        _pendingGuess = null;
        _guessCorrect = 0;
        _guessTotal = 0;
        UpdateSessionText();
    }

    private void UpdateSessionText()
    {
        ReplaySessionText.Text = _guessTotal == 0
            ? "Session: no guesses yet"
            : $"Session: {_guessCorrect}/{_guessTotal} correct ({(double)_guessCorrect / _guessTotal * 100:N0}%)";
    }

    #endregion
}
