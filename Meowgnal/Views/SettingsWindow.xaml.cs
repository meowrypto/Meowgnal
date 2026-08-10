using System.Windows;
using System.Windows.Controls;
using Meowgnal.Models;
using Meowgnal.Services;

namespace Meowgnal.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow()
    {
        InitializeComponent();
        _settings = SettingsStorageService.Load();

        SourceBinance.IsChecked = _settings.DefaultDataSource == "binance";
        SourceHyperliquid.IsChecked = _settings.DefaultDataSource == "hyperliquid";

        ApiKeyBox.Text = _settings.BinanceApiKey;
        ApiSecretBox.Password = _settings.BinanceApiSecret;

        ToastCheck.IsChecked = _settings.ToastNotificationsEnabled;
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

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PanelDataSources is null) return;
        PanelDataSources.Visibility = NavDataSources.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelApiKeys.Visibility = NavApiKeys.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelPaperTrading.Visibility = NavPaperTrading.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelNotifications.Visibility = NavNotifications.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelLicense.Visibility = NavLicense.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TestNotificationButton_Click(object sender, RoutedEventArgs e)
    {
        if (ToastCheck.IsChecked == true)
            NotificationService.ShowToast("Meowgnal — test", "Notifications are working correctly.");
        if (SoundCheck.IsChecked == true)
            NotificationService.PlayAlertSound();
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

    private void SaveAndClose()
    {
        _settings.DefaultDataSource = SourceHyperliquid.IsChecked == true ? "hyperliquid" : "binance";
        _settings.BinanceApiKey = ApiKeyBox.Text;
        _settings.BinanceApiSecret = ApiSecretBox.Password;
        _settings.ToastNotificationsEnabled = ToastCheck.IsChecked == true;
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