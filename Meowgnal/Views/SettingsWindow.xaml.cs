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

        Closing += (_, _) => SaveAndClose();
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PanelDataSources is null) return; // fires once during InitializeComponent, before panels exist
        PanelDataSources.Visibility = NavDataSources.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelApiKeys.Visibility = NavApiKeys.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelNotifications.Visibility = NavNotifications.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelLicense.Visibility = NavLicense.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    // Lets the user verify toast + sound right here, without waiting for a real signal.
    private void TestNotificationButton_Click(object sender, RoutedEventArgs e)
    {
        if (ToastCheck.IsChecked == true)
            NotificationService.ShowToast("Meowgnal — test", "Notifications are working correctly.");
        if (SoundCheck.IsChecked == true)
            NotificationService.PlayAlertSound();
    }

    private void SaveAndClose()
    {
        _settings.DefaultDataSource = SourceHyperliquid.IsChecked == true ? "hyperliquid" : "binance";
        _settings.BinanceApiKey = ApiKeyBox.Text;
        _settings.BinanceApiSecret = ApiSecretBox.Password;
        _settings.ToastNotificationsEnabled = ToastCheck.IsChecked == true;
        _settings.SoundNotificationsEnabled = SoundCheck.IsChecked == true;
        _settings.SignalCheckIntervalSeconds = IntervalCombo.SelectedItem is ComboBoxItem item
            && item.Tag is string tag
            && int.TryParse(tag, out var seconds)
            ? seconds
            : 60;
        SettingsStorageService.Save(_settings);
    }
}