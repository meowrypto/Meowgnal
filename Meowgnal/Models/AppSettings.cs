namespace Meowgnal.Models;

public sealed class AppSettings
{
    public string DefaultDataSource { get; set; } = "binance";

    // Stored encrypted on disk. Should ONLY ever be a Read-Only API key —
    // never one with trade or withdrawal permissions.
    public string BinanceApiKey { get; set; } = "";
    public string BinanceApiSecret { get; set; } = "";

    public bool ToastNotificationsEnabled { get; set; } = true;
    public bool SoundNotificationsEnabled { get; set; } = true;

    // How often the background monitor scans strategies for new signals.
    public int SignalCheckIntervalSeconds { get; set; } = 60;

    // Timeframes starred into the toolbar (max 6), TradingView style.
    public List<string> FavoriteTimeframes { get; set; } = new() { "15m", "1h", "4h", "1d", "1w", "1M" };
}