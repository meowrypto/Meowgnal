using System.Windows;
using System.Windows.Controls;
using Meowgnal.Models;
using Meowgnal.Services;
using System.Windows.Input;

namespace Meowgnal.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

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

    public SettingsWindow()
    {
        InitializeComponent();
        _settings = SettingsStorageService.Load();

        SourceBinance.IsChecked = _settings.DefaultDataSource == "binance";
        SourceHyperliquid.IsChecked = _settings.DefaultDataSource == "hyperliquid";

        ApiKeyBox.Text = _settings.BinanceApiKey;
        ApiSecretBox.Password = _settings.BinanceApiSecret;

        // Accuracy filters
        AccuracyClosedCandleCheck.IsChecked = _settings.AccuracyClosedCandleOnly;
        AccuracyMtfCheck.IsChecked = _settings.AccuracyMtfFilter;
        AccuracyVolumeCheck.IsChecked = _settings.AccuracyVolumeFilter;
        AccuracyVolumeMultiplierBox.Text = _settings.AccuracyVolumeMultiplier.ToString("0.0");
        AccuracyRegimeCheck.IsChecked = _settings.AccuracyRegimeFilter;
        UpdateVolumeMultiplierVisibility();

        ToastCheck.IsChecked = _settings.ToastNotificationsEnabled;
        TelegramBotTokenBox.Password = _settings.TelegramBotToken;
        TelegramChatIdBox.Text = _settings.TelegramChatId;
        SoundCheck.IsChecked = _settings.SoundNotificationsEnabled;
        IntervalCombo.SelectedIndex = _settings.SignalCheckIntervalSeconds switch
        {
            30 => 0,
            300 => 2,
            900 => 3,
            _ => 1
        };

        PaperBalanceBox.Text = _settings.PaperStartingBalance.ToString();
        PaperLeverageBox.Text = _settings.PaperDefaultLeverage.ToString();
        PaperTakerFeeBox.Text = _settings.PaperTakerFeePercent.ToString();
        PaperUseRiskSizingCheck.IsChecked = _settings.PaperUseRiskBasedSizing;
        PaperRiskPercentBox.Text = _settings.PaperRiskPercentPerTrade.ToString();
        PaperPositionSizeBox.Text = _settings.PaperPositionSizePercent.ToString();
        PaperDefaultSLBox.Text = _settings.PaperDefaultStopLossPercent.ToString();
        PaperDefaultTPBox.Text = _settings.PaperDefaultTakeProfitPercent.ToString();
        PaperMaxDailyLossBox.Text = _settings.PaperMaxDailyLossPercent.ToString();
        PaperMaxPositionsBox.Text = _settings.PaperMaxOpenPositions.ToString();

        UpdatePositionSizingVisibility();
        PaperUseRiskSizingCheck.Checked += (_, _) => UpdatePositionSizingVisibility();
        PaperUseRiskSizingCheck.Unchecked += (_, _) => UpdatePositionSizingVisibility();

        Closing += (_, _) => SaveAndClose();
    }

    private void UpdatePositionSizingVisibility()
    {
        var useRisk = PaperUseRiskSizingCheck.IsChecked == true;
        RiskSizingPanel.Visibility = useRisk ? Visibility.Visible : Visibility.Collapsed;
        FixedSizePanel.Visibility = useRisk ? Visibility.Collapsed : Visibility.Visible;
    }

    private void AccuracyVolumeCheck_Changed(object sender, RoutedEventArgs e)
    {
        UpdateVolumeMultiplierVisibility();
    }

    private void UpdateVolumeMultiplierVisibility()
    {
        if (VolumeMultiplierPanel is null) return;
        VolumeMultiplierPanel.Visibility = AccuracyVolumeCheck.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PanelDataSources is null) return;
        PanelDataSources.Visibility = NavDataSources.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelApiKeys.Visibility = NavApiKeys.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelAccuracy.Visibility = NavAccuracy.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelPaperTrading.Visibility = NavPaperTrading.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelNotifications.Visibility = NavNotifications.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelChecklist.Visibility = NavChecklist.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    }

    private void TestNotificationButton_Click(object sender, RoutedEventArgs e)
    {
        if (ToastCheck.IsChecked == true)
            NotificationService.ShowToast("Meowgnal — test", "Notifications are working correctly.");
        if (SoundCheck.IsChecked == true)
        {
            
        }
            NotificationService.PlayAlertSound();
     }
        


    private async void TestTelegramButton_Click(object sender, RoutedEventArgs e)
    {
        TelegramStatusText.Text = "Sending...";
        TelegramStatusText.Foreground = (System.Windows.Media.Brush)FindResource("TextMuted");

        // Temporarily save so the service can read the new values
        var tempSettings = SettingsStorageService.Load();
        tempSettings.TelegramBotToken = TelegramBotTokenBox.Password;
        tempSettings.TelegramChatId = TelegramChatIdBox.Text;
        SettingsStorageService.Save(tempSettings);

        var success = await TelegramNotificationService.SendAsync(
            "🐱 *Meowgnal Test*\nTelegram notifications are working correctly!");

        if (success)
        {
            TelegramStatusText.Text = "✓ Message sent!";
            TelegramStatusText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessColor");
        }
        else
        {
            TelegramStatusText.Text = "✕ Failed (check token/chat ID)";
            TelegramStatusText.Foreground = (System.Windows.Media.Brush)FindResource("DangerColor");
        }
    }
    

    private void ResetPaperAccount_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "This will permanently delete all paper trading history and reset your balance.\n\n" +
            "This action cannot be undone. Continue?",
            "Reset Paper Account",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        var account = new PaperAccountFile
        {
            StartingBalance = _settings.PaperStartingBalance,
            CurrentBalance = _settings.PaperStartingBalance
        };
        PaperAccountStorageService.Save(account);
        NotificationService.ShowToast("Meowgnal", "Paper account has been reset. Restart the app to see the new balance.");
    }

    private void EditDefaultChecklist_Click(object sender, RoutedEventArgs e)
    {
        var win = new ChecklistEditorWindow(_settings.DefaultChecklist) { Owner = this };
        if (win.ShowDialog() == true)
            _settings.DefaultChecklist = win.EditedList;
    }

    private void SaveAndClose()
    {
        _settings.DefaultDataSource = SourceHyperliquid.IsChecked == true ? "hyperliquid" : "binance";
        _settings.BinanceApiKey = ApiKeyBox.Text;
        _settings.BinanceApiSecret = ApiSecretBox.Password;

        // Accuracy filters
        _settings.AccuracyClosedCandleOnly = AccuracyClosedCandleCheck.IsChecked == true;
        _settings.AccuracyMtfFilter = AccuracyMtfCheck.IsChecked == true;
        _settings.AccuracyVolumeFilter = AccuracyVolumeCheck.IsChecked == true;
        if (double.TryParse(AccuracyVolumeMultiplierBox.Text, out var multiplier))
            _settings.AccuracyVolumeMultiplier = multiplier;
        _settings.AccuracyRegimeFilter = AccuracyRegimeCheck.IsChecked == true;

        _settings.ToastNotificationsEnabled = ToastCheck.IsChecked == true;
        _settings.TelegramBotToken = TelegramBotTokenBox.Password;
        _settings.TelegramChatId = TelegramChatIdBox.Text;
        _settings.SoundNotificationsEnabled = SoundCheck.IsChecked == true;
        _settings.SignalCheckIntervalSeconds = IntervalCombo.SelectedItem is ComboBoxItem item
            && item.Tag is string tag && int.TryParse(tag, out var seconds) ? seconds : 60;

        if (decimal.TryParse(PaperBalanceBox.Text, out var balance)) _settings.PaperStartingBalance = balance;
        if (decimal.TryParse(PaperLeverageBox.Text, out var leverage)) _settings.PaperDefaultLeverage = leverage;
        if (decimal.TryParse(PaperTakerFeeBox.Text, out var fee)) _settings.PaperTakerFeePercent = fee;
        _settings.PaperUseRiskBasedSizing = PaperUseRiskSizingCheck.IsChecked == true;
        if (decimal.TryParse(PaperRiskPercentBox.Text, out var risk)) _settings.PaperRiskPercentPerTrade = risk;
        if (decimal.TryParse(PaperPositionSizeBox.Text, out var posSize)) _settings.PaperPositionSizePercent = posSize;
        if (decimal.TryParse(PaperDefaultSLBox.Text, out var sl)) _settings.PaperDefaultStopLossPercent = sl;
        if (decimal.TryParse(PaperDefaultTPBox.Text, out var tp)) _settings.PaperDefaultTakeProfitPercent = tp;
        if (decimal.TryParse(PaperMaxDailyLossBox.Text, out var maxLoss)) _settings.PaperMaxDailyLossPercent = maxLoss;
        if (int.TryParse(PaperMaxPositionsBox.Text, out var maxPos)) _settings.PaperMaxOpenPositions = maxPos;

        SettingsStorageService.Save(_settings);
    }
}