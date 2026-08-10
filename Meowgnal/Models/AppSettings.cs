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

    // ------------------------------------------------------------------
    // Paper Trading Settings (Category B)
    // ------------------------------------------------------------------

    /// <summary>
    /// Initial balance when resetting the paper account.
    /// </summary>
    public decimal PaperStartingBalance { get; set; } = 10000m;

    /// <summary>
    /// If true, use risk-based position sizing (risk X% per trade based on SL distance).
    /// If false, use fixed percentage of balance.
    /// </summary>
    public bool PaperUseRiskBasedSizing { get; set; } = true;

    /// <summary>
    /// Maximum percentage of balance to risk per trade (only used when risk-based sizing is on).
    /// Professional standard: 1-2%.
    /// </summary>
    public decimal PaperRiskPercentPerTrade { get; set; } = 2m;

    /// <summary>
    /// Percentage of balance to use per position (only used when risk-based sizing is OFF).
    /// </summary>
    public decimal PaperPositionSizePercent { get; set; } = 10m;

    /// <summary>
    /// Default leverage for new positions (1x = no leverage).
    /// </summary>
    public decimal PaperDefaultLeverage { get; set; } = 10m;

    /// <summary>
    /// Default stop-loss distance percentage from entry (used for signals without explicit SL).
    /// </summary>
    public decimal PaperDefaultStopLossPercent { get; set; } = 2m;

    /// <summary>
    /// Default take-profit distance percentage from entry (used for signals without explicit TP).
    /// </summary>
    public decimal PaperDefaultTakeProfitPercent { get; set; } = 4m;

    /// <summary>
    /// If daily realized loss exceeds this percentage, all positions are closed and
    /// trading is suspended until the next UTC day. Set to 0 to disable.
    /// </summary>
    public decimal PaperMaxDailyLossPercent { get; set; } = 5m;

    /// <summary>
    /// Maximum number of concurrent open positions. 0 = unlimited.
    /// </summary>
    public int PaperMaxOpenPositions { get; set; } = 5;

    /// <summary>
    /// Taker fee percentage applied on each trade (entry + exit).
    /// Binance Futures standard: 0.04% per side.
    /// </summary>
    public decimal PaperTakerFeePercent { get; set; } = 0.04m;
}