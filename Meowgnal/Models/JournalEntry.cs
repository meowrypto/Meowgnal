using System;
using System.Collections.Generic;

namespace Meowgnal.Models;

public class JournalEntry
{
    public string EntryId { get; set; } = Guid.NewGuid().ToString("N");
    public string PositionId { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string DataSource { get; set; } = "binance";
    public PositionSide Side { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal Size { get; set; }
    public decimal PnL { get; set; }
    public decimal RoiPercent { get; set; }
    public CloseReason Reason { get; set; }
    public DateTime OpenTime { get; set; }
    public DateTime CloseTime { get; set; }
    public string? StrategyId { get; set; }
    public ChecklistResult? ChecklistResult { get; set; }

    // Trade autopsy: combined plain-English reason for entry + exit.
    public string TradeExplanation { get; set; } = "";

    // Journal-specific fields
    public string Notes { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public string ScreenshotPath { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static JournalEntry FromPaperTrade(PaperTrade trade)
    {
        return new JournalEntry
        {
            PositionId = trade.PositionId,
            Symbol = trade.Symbol,
            DataSource = trade.DataSource,
            Side = trade.Side,
            EntryPrice = trade.EntryPrice,
            ExitPrice = trade.ExitPrice,
            Size = trade.Size,
            PnL = trade.PnL,
            RoiPercent = trade.RoiPercent,
            Reason = trade.Reason,
            OpenTime = trade.OpenTime,
            CloseTime = trade.CloseTime,
            StrategyId = trade.StrategyId,
            ChecklistResult = trade.ChecklistResult,
            TradeExplanation = BuildTradeExplanation(trade)
        };
    }

    private static string BuildTradeExplanation(PaperTrade trade)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(trade.EntryExplanation)) parts.Add(trade.EntryExplanation);
        if (!string.IsNullOrWhiteSpace(trade.ExitExplanation)) parts.Add(trade.ExitExplanation);
        return parts.Count == 0 ? "" : string.Join(" ", parts);
    }
}

public class JournalFile
{
    public List<JournalEntry> Entries { get; set; } = [];
}