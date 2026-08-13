using System.Collections.Generic;
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

    // Status-bar clock
    public string ClockMode { get; set; } = "utc";
    public string ClockTimeZoneId { get; set; } = "";

    // Theme settings
    public string Theme { get; set; } = "dark"; // dark, light, system, custom
    public string CustomBackground { get; set; } = "#131722";
    public string CustomPanel { get; set; } = "#1E222D";
    public string CustomBorder { get; set; } = "#2A2E39";
    public string CustomTextPrimary { get; set; } = "#D1D4DC";
    public string CustomAccent { get; set; } = "#2962FF";

    // Chart appearance overrides (empty = follow the active theme)
    public string ChartUpColor { get; set; } = "";
    public string ChartDownColor { get; set; } = "";
    public string ChartBackgroundColor { get; set; } = "";
    public string ChartGridColor { get; set; } = "";
    public string ChartBorderColor { get; set; } = "";
    public string ChartCrosshairColor { get; set; } = "";

    // Phase 34 — Accuracy Engine
    public bool AccuracyClosedCandleOnly { get; set; } = true;
    public bool AccuracyMtfFilter { get; set; } = true;
    public bool AccuracyVolumeFilter { get; set; } = false;
    public double AccuracyVolumeMultiplier { get; set; } = 1.5;
    public bool AccuracyRegimeFilter { get; set; } = false;
    // Phase 25 — Multi-strategy Portfolio
    public List<string> PortfolioEnabledStrategyIds { get; set; } = [];
    public int PortfolioMaxTotalPositions { get; set; } = 3;
    public int PortfolioMaxPositionsPerStrategy { get; set; } = 1;

    // Phase 27 — Hide all drawings toggle (eye button)
    public bool DrawingsHidden { get; set; }

    // Profile & onboarding
    public bool FirstRunCompleted { get; set; } = false;
    public string ProfileName { get; set; } = "";
    public string ProfileAvatar { get; set; } = "🐱";
    public bool IsGuest { get; set; } = true;
    public DateTime DemoStartDate { get; set; } = DateTime.MinValue;
    public int DemoTrialDays { get; set; } = 14;
    public string LicenseKey { get; set; } = "";
}
