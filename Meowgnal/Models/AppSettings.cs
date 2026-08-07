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
}