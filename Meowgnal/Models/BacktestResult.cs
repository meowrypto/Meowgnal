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
}