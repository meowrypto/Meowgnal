using System;
using Meowgnal.Models;

namespace Meowgnal.Services;

public static class PaperTradingEngine
{
    public const decimal MaintenanceMarginRate = 0.005m;

    public static void CheckDailyReset(PaperAccountFile account)
    {
        var today = DateTime.UtcNow.Date;
        if (account.DailyResetDate.Date != today)
        {
            account.DailyResetDate = today;
            account.DailyRealizedPnL = 0m;
            account.IsSuspendedUntilTomorrow = false;
        }
    }

    public static bool DailyLossLimitBreached(PaperAccountFile account, AppSettings settings)
    {
        if (settings.PaperMaxDailyLossPercent <= 0) return false;
        var limit = account.StartingBalance * settings.PaperMaxDailyLossPercent / 100m;
        return account.DailyRealizedPnL <= -limit;
    }

    public sealed record OpenResult(bool Ok, string Error, PaperPosition? Position)
    {
        public static OpenResult Fail(string error) => new(false, error, null);
    }

    public static OpenResult TryOpen(
        PaperAccountFile account, AppSettings settings, string symbol, string dataSource,
        PositionSide side, decimal entryPrice, decimal leverage,
        decimal stopLossPrice, decimal takeProfitPrice,
        bool trailingEnabled, decimal trailingDistancePercent, decimal trailingActivationPercent,
        decimal customMarginUsdt = 0m, string? strategyId = null)
    {
        CheckDailyReset(account);
        if (account.IsSuspendedUntilTomorrow)
            return OpenResult.Fail("Trading is suspended until tomorrow (max daily loss reached).");
        if (entryPrice <= 0) return OpenResult.Fail("Invalid entry price.");

        leverage = Math.Clamp(leverage, 1m, 125m);

        if (settings.PaperMaxOpenPositions > 0 &&
            account.OpenPositions.Count >= settings.PaperMaxOpenPositions)
            return OpenResult.Fail($"Maximum open positions reached ({settings.PaperMaxOpenPositions}).");

        if (stopLossPrice > 0)
        {
            if (side == PositionSide.Long && stopLossPrice >= entryPrice)
                return OpenResult.Fail("For a LONG position the stop-loss must be BELOW the entry price.");
            if (side == PositionSide.Short && stopLossPrice <= entryPrice)
                return OpenResult.Fail("For a SHORT position the stop-loss must be ABOVE the entry price.");
        }
        if (takeProfitPrice > 0)
        {
            if (side == PositionSide.Long && takeProfitPrice <= entryPrice)
                return OpenResult.Fail("For a LONG position the take-profit must be ABOVE the entry price.");
            if (side == PositionSide.Short && takeProfitPrice >= entryPrice)
                return OpenResult.Fail("For a SHORT position the take-profit must be BELOW the entry price.");
        }

        var fee = settings.PaperTakerFeePercent;
        decimal margin;
        if (customMarginUsdt > 0) margin = customMarginUsdt;
        else if (settings.PaperUseRiskBasedSizing && stopLossPrice > 0)
        {
            var riskAmount = account.CurrentBalance * settings.PaperRiskPercentPerTrade / 100m;
            var distancePerUnit = Math.Abs(entryPrice - stopLossPrice);
            var riskSize = distancePerUnit > 0 ? riskAmount / distancePerUnit : 0m;
            margin = riskSize * entryPrice / leverage;
        }
        else
        {
            margin = account.CurrentBalance * settings.PaperPositionSizePercent / 100m;
        }

        var maxMargin = account.CurrentBalance / (1m + leverage * fee / 100m);
        if (margin > maxMargin) margin = maxMargin;
        margin = Math.Round(margin, 2);
        if (margin <= 0) return OpenResult.Fail("Insufficient balance for this position.");

        var notional = margin * leverage;
        var size = Math.Round(notional / entryPrice, 8);
        if (size <= 0) return OpenResult.Fail("Computed position size is zero.");

        var entryFee = notional * fee / 100m;

        var position = new PaperPosition
        {
            Symbol = symbol,
            DataSource = dataSource,
            Side = side,
            EntryPrice = entryPrice,
            Size = size,
            Leverage = leverage,
            Margin = margin,
            EntryFee = entryFee,
            StopLoss = stopLossPrice,
            TakeProfit = takeProfitPrice,
            LiquidationPrice = side == PositionSide.Long
                ? Math.Max(0m, entryPrice * (1m - 1m / leverage + MaintenanceMarginRate))
                : entryPrice * (1m + 1m / leverage - MaintenanceMarginRate),
            TrailingEnabled = trailingEnabled,
            TrailingDistancePercent = trailingDistancePercent,
            TrailingActivationPercent = trailingActivationPercent,
            HighestPriceSinceEntry = entryPrice,
            LowestPriceSinceEntry = entryPrice,
            OpenTime = DateTime.UtcNow,
            StrategyId = strategyId,
        };

        account.CurrentBalance -= margin + entryFee;
        account.OpenPositions.Add(position);
        return new OpenResult(true, "", position);
    }

    public static void UpdateTrailing(PaperPosition position, decimal currentPrice)
    {
        if (!position.TrailingEnabled || position.TrailingDistancePercent <= 0) return;
        if (currentPrice > position.HighestPriceSinceEntry) position.HighestPriceSinceEntry = currentPrice;
        if (currentPrice < position.LowestPriceSinceEntry) position.LowestPriceSinceEntry = currentPrice;

        if (position.Side == PositionSide.Long)
        {
            var profitPct = (position.HighestPriceSinceEntry - position.EntryPrice) / position.EntryPrice * 100m;
            if (profitPct >= position.TrailingActivationPercent)
            {
                var desired = position.HighestPriceSinceEntry * (1m - position.TrailingDistancePercent / 100m);
                if (desired > position.TrailingCurrentStop) position.TrailingCurrentStop = desired;
            }
        }
        else
        {
            var profitPct = (position.EntryPrice - position.LowestPriceSinceEntry) / position.EntryPrice * 100m;
            if (profitPct >= position.TrailingActivationPercent)
            {
                var desired = position.LowestPriceSinceEntry * (1m + position.TrailingDistancePercent / 100m);
                if (position.TrailingCurrentStop == 0 || desired < position.TrailingCurrentStop)
                    position.TrailingCurrentStop = desired;
            }
        }
    }

    public static decimal EffectiveStopLoss(PaperPosition position)
    {
        if (!position.TrailingEnabled || position.TrailingCurrentStop == 0) return position.StopLoss;
        if (position.StopLoss == 0) return position.TrailingCurrentStop;
        return position.Side == PositionSide.Long
            ? Math.Max(position.StopLoss, position.TrailingCurrentStop)
            : Math.Min(position.StopLoss, position.TrailingCurrentStop);
    }

    public static CloseReason? CheckStops(PaperPosition position, decimal checkHigh, decimal checkLow)
    {
        if (position.Side == PositionSide.Long)
        {
            if (checkLow <= position.LiquidationPrice) return CloseReason.Liquidation;
            var sl = EffectiveStopLoss(position);
            if (sl > 0 && checkLow <= sl)
            {
                var byTrailing = position.TrailingEnabled && position.TrailingCurrentStop == sl &&
                                 (position.StopLoss == 0 || position.TrailingCurrentStop > position.StopLoss);
                return byTrailing ? CloseReason.TrailingStop : CloseReason.StopLoss;
            }
            if (position.TakeProfit > 0 && checkHigh >= position.TakeProfit) return CloseReason.TakeProfit;
        }
        else
        {
            if (checkHigh >= position.LiquidationPrice) return CloseReason.Liquidation;
            var sl = EffectiveStopLoss(position);
            if (sl > 0 && checkHigh >= sl)
            {
                var byTrailing = position.TrailingEnabled && position.TrailingCurrentStop == sl &&
                                 (position.StopLoss == 0 || position.TrailingCurrentStop < position.StopLoss);
                return byTrailing ? CloseReason.TrailingStop : CloseReason.StopLoss;
            }
            if (position.TakeProfit > 0 && checkLow <= position.TakeProfit) return CloseReason.TakeProfit;
        }
        return null;
    }

    public static PaperTrade Close(PaperAccountFile account, PaperPosition position,
        decimal exitPrice, CloseReason reason, decimal takerFeePercent)
    {
        decimal gross, exitFee, netPnL;

        if (reason == CloseReason.Liquidation)
        {
            exitPrice = position.LiquidationPrice;
            gross = -position.Margin;
            exitFee = 0m;
            netPnL = -position.Margin - position.EntryFee;
        }
        else
        {
            gross = position.Side == PositionSide.Long
                ? (exitPrice - position.EntryPrice) * position.Size
                : (position.EntryPrice - exitPrice) * position.Size;
            exitFee = exitPrice * position.Size * takerFeePercent / 100m;
            netPnL = gross - exitFee - position.EntryFee;
            account.CurrentBalance += position.Margin + gross - exitFee;
        }

        account.OpenPositions.Remove(position);

        var trade = new PaperTrade
        {
            PositionId = position.PositionId,
            Symbol = position.Symbol,
            DataSource = position.DataSource,
            Side = position.Side,
            EntryPrice = position.EntryPrice,
            ExitPrice = exitPrice,
            Size = position.Size,
            Leverage = position.Leverage,
            Margin = position.Margin,
            PnL = netPnL,
            RoiPercent = position.Margin > 0 ? netPnL / position.Margin * 100m : 0m,
            Fees = position.EntryFee + exitFee,
            Reason = reason,
            OpenTime = position.OpenTime,
            CloseTime = DateTime.UtcNow,
            StrategyId = position.StrategyId,
        };

        account.TradeHistory.Insert(0, trade);
        if (account.TradeHistory.Count > 200)
            account.TradeHistory.RemoveRange(200, account.TradeHistory.Count - 200);
        account.DailyRealizedPnL += netPnL;

        // Auto-create journal entry for every closed trade
        var journalEntry = JournalEntry.FromPaperTrade(trade);
        JournalStorageService.AddEntry(journalEntry);

        return trade;
    }

    public static decimal Equity(PaperAccountFile account, Func<PaperPosition, decimal> priceOf, decimal takerFeePercent)
    {
        var equity = account.CurrentBalance;
        foreach (var p in account.OpenPositions)
            equity += p.Margin + p.UnrealizedPnL(priceOf(p), takerFeePercent);
        return equity;
    }
}