using System;
using System.Collections.Generic;

namespace Meowgnal.Models;

public sealed class BacktestTrade
{
    public DateTime EntryTime { get; set; }
    public decimal EntryPrice { get; set; }
    public DateTime ExitTime { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal StopLossPrice { get; set; }
    public decimal TargetPrice { get; set; }
    public string ExitReason { get; set; } = ""; // "stopLoss" | "target" | "signal"
    public decimal PnL { get; set; }
    public double PnLPercent { get; set; }

    // Trade autopsy: real indicator values at entry + plain-English explanation.
    public Dictionary<string, decimal> IndicatorSnapshotAtEntry { get; set; } = new();
    public string EntryExplanation { get; set; } = "";
    public string ExitExplanation { get; set; } = "";
}

public sealed class MonthlyPerformance
{
    public string MonthLabel { get; set; } = ""; // e.g. "2025-08"
    public int TradeCount { get; set; }
    public decimal NetPnL { get; set; }
    public double WinRatePercent { get; set; }
}

public sealed class BacktestResult
{
    public List<BacktestTrade> Trades { get; set; } = new();
    public List<(DateTime Time, decimal Balance)> EquityCurve { get; set; } = new();
    public decimal StartingBalance { get; set; }
    public decimal FinalBalance { get; set; }
    public double WinRatePercent { get; set; }
    public double AverageRiskReward { get; set; }
    public double MaxDrawdownPercent { get; set; }

    // Phase 24 — Performance Analytics
    public double SharpeRatio { get; set; }
    public double SortinoRatio { get; set; }
    public List<MonthlyPerformance> MonthlyBreakdown { get; set; } = new();

    // Statistical confidence: "low" (<30), "moderate" (30-99), "reliable" (100+)
    public string SampleSizeWarning { get; set; } = "reliable";

    // Backtest results split by detected market regime (Bull / Bear / Sideways).
    public sealed class RegimeBacktestResult
    {
        public BacktestResult Overall { get; set; } = new();
        public BacktestResult BullMarket { get; set; } = new();
        public BacktestResult BearMarket { get; set; } = new();
        public BacktestResult SidewaysMarket { get; set; } = new();

        // False when the bar range is too short to detect regimes reliably.
        public bool HasRegimeData { get; set; }
    }
}