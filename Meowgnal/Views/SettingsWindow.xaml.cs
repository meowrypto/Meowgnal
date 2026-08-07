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

    private void SaveAndClose()
    {
        _settings.DefaultDataSource = SourceHyperliquid.IsChecked == true ? "hyperliquid" : "binance";
        _settings.BinanceApiKey = ApiKeyBox.Text;
        _settings.BinanceApiSecret = ApiSecretBox.Password;
        _settings.ToastNotificationsEnabled = ToastCheck.IsChecked == true;
        _settings.SoundNotificationsEnabled = SoundCheck.IsChecked == true;
        SettingsStorageService.Save(_settings);
    }
}