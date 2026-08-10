using System;
using System.Collections.Generic;

namespace Meowgnal.Models;

/// <summary>
/// Side (direction) of a paper trading position.
/// </summary>
public enum PositionSide
{
    Long,
    Short
}

/// <summary>
/// Reason why a position was closed.
/// </summary>
public enum CloseReason
{
    TakeProfit,
    StopLoss,
    Liquidation,
    Manual,
    SignalExit,
    RiskRule,
    TrailingStop
}

/// <summary>
/// An open paper-trading position with live fields updated on each tick.
/// </summary>
public class PaperPosition
{
    public string PositionId { get; set; } = Guid.NewGuid().ToString("N");
    public string Symbol { get; set; } = "";
    public string DataSource { get; set; } = "binance";
    public PositionSide Side { get; set; } = PositionSide.Long;
    public decimal EntryPrice { get; set; }
    public decimal Size { get; set; }           // quantity of the base asset
    public decimal Leverage { get; set; } = 1m;
    public decimal Margin { get; set; }          // locked capital = (Size * Entry) / Leverage
    public decimal StopLoss { get; set; }        // 0 = no SL
    public decimal TakeProfit { get; set; }      // 0 = no TP
    public decimal LiquidationPrice { get; set; }

    // Trailing stop state
    public bool TrailingEnabled { get; set; }
    public decimal TrailingDistancePercent { get; set; }  // e.g. 2 = 2%
    public decimal TrailingActivationPercent { get; set; } // activate after X% profit
    public decimal TrailingCurrentStop { get; set; }       // live SL moved by engine
    public decimal HighestPriceSinceEntry { get; set; }    // for longs
    public decimal LowestPriceSinceEntry { get; set; }     // for shorts

    public DateTime OpenTime { get; set; } = DateTime.UtcNow;
    public decimal EntryFee { get; set; }        // paid at open (taker fee)
    public string? StrategyId { get; set; }      // if opened by a strategy signal

    /// <summary>
    /// Unrealized PnL given the current market price.
    /// Long: (current - entry) * size - exit fee
    /// Short: (entry - current) * size - exit fee
    /// </summary>
    public decimal UnrealizedPnL(decimal currentPrice, decimal takerFeePercent)
    {
        var gross = Side == PositionSide.Long
            ? (currentPrice - EntryPrice) * Size
            : (EntryPrice - currentPrice) * Size;
        var exitFee = currentPrice * Size * (takerFeePercent / 100m);
        return gross - EntryFee - exitFee;
    }

    /// <summary>
    /// Unrealized ROI% based on MARGIN (not balance) — the correct way with leverage.
    /// </summary>
    public decimal UnrealizedRoiPercent(decimal currentPrice, decimal takerFeePercent)
    {
        if (Margin <= 0) return 0;
        return (UnrealizedPnL(currentPrice, takerFeePercent) / Margin) * 100m;
    }
}

/// <summary>
/// A closed paper-trading trade (historical record).
/// </summary>
public class PaperTrade
{
    public string PositionId { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string DataSource { get; set; } = "binance";
    public PositionSide Side { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal Size { get; set; }
    public decimal Leverage { get; set; }
    public decimal Margin { get; set; }
    public decimal PnL { get; set; }
    public decimal RoiPercent { get; set; }
    public decimal Fees { get; set; }
    public CloseReason Reason { get; set; }
    public DateTime OpenTime { get; set; }
    public DateTime CloseTime { get; set; }
    public string? StrategyId { get; set; }
}

/// <summary>
/// Full paper-trading account (persisted encrypted via DPAPI).
/// </summary>
public class PaperAccountFile
{
    public decimal StartingBalance { get; set; } = 10000m;
    public decimal CurrentBalance { get; set; } = 10000m;
    public List<PaperPosition> OpenPositions { get; set; } = new();
    public List<PaperTrade> TradeHistory { get; set; } = new();

    // Daily risk tracking (for Max Daily Loss rule)
    public DateTime DailyResetDate { get; set; } = DateTime.UtcNow.Date;
    public decimal DailyRealizedPnL { get; set; }
    public bool IsSuspendedUntilTomorrow { get; set; }
}