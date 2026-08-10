using System;
using System.Collections.Generic;

namespace Meowgnal.Models;

public enum PositionSide { Long, Short }

public enum CloseReason
{
    TakeProfit, StopLoss, Liquidation, Manual, SignalExit, RiskRule, TrailingStop
}

public class PaperPosition
{
    public string PositionId { get; set; } = Guid.NewGuid().ToString("N");
    public string Symbol { get; set; } = "";
    public string DataSource { get; set; } = "binance";
    public PositionSide Side { get; set; } = PositionSide.Long;
    public decimal EntryPrice { get; set; }
    public decimal Size { get; set; }
    public decimal Leverage { get; set; } = 1m;
    public decimal Margin { get; set; }
    public decimal StopLoss { get; set; }
    public decimal TakeProfit { get; set; }
    public decimal LiquidationPrice { get; set; }

    public bool TrailingEnabled { get; set; }
    public decimal TrailingDistancePercent { get; set; }
    public decimal TrailingActivationPercent { get; set; }
    public decimal TrailingCurrentStop { get; set; }
    public decimal HighestPriceSinceEntry { get; set; }
    public decimal LowestPriceSinceEntry { get; set; }

    public DateTime OpenTime { get; set; } = DateTime.UtcNow;
    public decimal EntryFee { get; set; }
    public string? StrategyId { get; set; }

    public decimal UnrealizedPnL(decimal currentPrice, decimal takerFeePercent)
    {
        var gross = Side == PositionSide.Long
            ? (currentPrice - EntryPrice) * Size
            : (EntryPrice - currentPrice) * Size;
        var exitFee = currentPrice * Size * (takerFeePercent / 100m);
        return gross - EntryFee - exitFee;
    }

    public decimal UnrealizedRoiPercent(decimal currentPrice, decimal takerFeePercent)
    {
        if (Margin <= 0) return 0;
        return (UnrealizedPnL(currentPrice, takerFeePercent) / Margin) * 100m;
    }
}

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

public class PaperAccountFile
{
    public decimal StartingBalance { get; set; } = 10000m;
    public decimal CurrentBalance { get; set; } = 10000m;
    public List<PaperPosition> OpenPositions { get; set; } = new();
    public List<PaperTrade> TradeHistory { get; set; } = new();
    public DateTime DailyResetDate { get; set; } = DateTime.UtcNow.Date;
    public decimal DailyRealizedPnL { get; set; }
    public bool IsSuspendedUntilTomorrow { get; set; }
}