namespace Meowgnal.Models;

public sealed class AppSettings
{
    public string DefaultDataSource { get; set; } = "binance";
    public string BinanceApiKey { get; set; } = "";
    public string BinanceApiSecret { get; set; } = "";

    public bool ToastNotificationsEnabled { get; set; } = true;
    public bool SoundNotificationsEnabled { get; set; } = true;
    public int SignalCheckIntervalSeconds { get; set; } = 60;

    public List<string> FavoriteTimeframes { get; set; } = new() { "15m", "1h", "4h", "1d", "1w", "1M" };

    // ------------------------------------------------------------------
    // Paper Trading Settings
    // ------------------------------------------------------------------
    public decimal PaperStartingBalance { get; set; } = 10000m;
    public bool PaperUseRiskBasedSizing { get; set; } = true;
    public decimal PaperRiskPercentPerTrade { get; set; } = 2m;
    public decimal PaperPositionSizePercent { get; set; } = 10m;
    public decimal PaperDefaultLeverage { get; set; } = 10m;
    public decimal PaperDefaultStopLossPercent { get; set; } = 2m;
    public decimal PaperDefaultTakeProfitPercent { get; set; } = 4m;
    public decimal PaperMaxDailyLossPercent { get; set; } = 5m;
    public int PaperMaxOpenPositions { get; set; } = 5;
    public decimal PaperTakerFeePercent { get; set; } = 0.04m;
    public bool PaperAutoTradeEnabled { get; set; } = true;

    // Status-bar clock display mode: "utc" | "system" | "custom".
    public string ClockMode { get; set; } = "utc";

    // Windows time-zone id used when ClockMode == "custom".
    public string ClockTimeZoneId { get; set; } = "";
}