using System;
using System.Collections.Generic;
using System.Linq;
using FacioQuo.Stock.Indicators;
using Meowgnal.Models;
using Bar = Meowgnal.Models.Bar;

namespace Meowgnal.Engine;

public static class BacktestEngine
{
    public static BacktestResult Run(
        StrategyDefinition strategy,
        IReadOnlyList<Bar> bars,
        decimal startingBalance,
        decimal feePercent,
        decimal slippagePercent)
    {
        var series = RuleEngine.CalculateIndicatorSeries(bars, strategy.Indicators);
        var atr = bars.ToAtr(14).Select(r => r.Atr).ToArray();

        var trades = new List<BacktestTrade>();
        var equityCurve = new List<(DateTime, decimal)>();
        var balance = startingBalance;
        var peakBalance = startingBalance;
        var maxDrawdown = 0.0;

        var inPosition = false;
        var entryIndex = -1;
        decimal entryPrice = 0, stopLoss = 0, target = 0, quantity = 0;
        var prevEntrySignal = false;
        var prevExitSignal = false;

        for (var i = 0; i < bars.Count; i++)
        {
            var entryNow = RuleEngine.EvaluateRuleGroup(strategy.EntryRules, i, bars, series);
            var exitNow = RuleEngine.EvaluateRuleGroup(strategy.ExitRules, i, bars, series);
            var fireEntry = strategy.EntryRules.TriggerMode == "everyCandle" ? entryNow : entryNow && !prevEntrySignal;
            var fireExit = strategy.ExitRules.TriggerMode == "everyCandle" ? exitNow : exitNow && !prevExitSignal;
            prevEntrySignal = entryNow;
            prevExitSignal = exitNow;

            if (!inPosition && fireEntry && i + 1 < bars.Count)
            {
                var nextBar = bars[i + 1];
                entryPrice = nextBar.Open * (1 + slippagePercent / 100m);

                var atrValue = i < atr.Length ? atr[i] : null;
                var stopDistance = atrValue.HasValue
                    ? (decimal)atrValue.Value * (decimal)strategy.RiskManagement.StopLoss.Multiplier
                    : entryPrice * 0.02m;

                stopLoss = entryPrice - stopDistance;
                target = entryPrice + stopDistance * (decimal)strategy.RiskManagement.Target.Value;

                var riskAmount = balance * (decimal)strategy.RiskManagement.PositionSizing.RiskPercentPerTrade / 100m;
                quantity = stopDistance > 0 ? riskAmount / stopDistance : 0;

                inPosition = true;
                entryIndex = i + 1;
                continue;
            }

            if (inPosition && i >= entryIndex)
            {
                var bar = bars[i];
                string? exitReason = null;
                var exitPrice = 0m;

                if (bar.Low <= stopLoss) { exitReason = "stopLoss"; exitPrice = stopLoss; }
                else if (bar.High >= target) { exitReason = "target"; exitPrice = target; }
                else if (fireExit) { exitReason = "signal"; exitPrice = bar.Close; }

                if (exitReason is not null)
                {
                    exitPrice *= (1 - slippagePercent / 100m);
                    var grossPnl = (exitPrice - entryPrice) * quantity;
                    var fees = (entryPrice * quantity + exitPrice * quantity) * (feePercent / 100m);
                    var netPnl = grossPnl - fees;

                    balance += netPnl;
                    trades.Add(new BacktestTrade
                    {
                        EntryTime = bars[entryIndex].Timestamp,
                        EntryPrice = entryPrice,
                        ExitTime = bar.Timestamp,
                        ExitPrice = exitPrice,
                        StopLossPrice = stopLoss,
                        TargetPrice = target,
                        ExitReason = exitReason,
                        PnL = netPnl,
                        PnLPercent = (double)((exitPrice - entryPrice) / entryPrice * 100m)
                    });

                    inPosition = false;
                    peakBalance = Math.Max(peakBalance, balance);
                    var drawdown = peakBalance > 0 ? (double)((peakBalance - balance) / peakBalance * 100m) : 0;
                    maxDrawdown = Math.Max(maxDrawdown, drawdown);
                }
            }

            equityCurve.Add((bars[i].Timestamp, balance));
        }

        var wins = trades.Count(t => t.PnL > 0);
        var winRate = trades.Count > 0 ? (double)wins / trades.Count * 100 : 0;
        var avgRR = trades.Count > 0
            ? trades.Average(t => Math.Abs(t.EntryPrice - t.StopLossPrice) != 0
                ? (double)(Math.Abs(t.ExitPrice - t.EntryPrice) / Math.Abs(t.EntryPrice - t.StopLossPrice))
                : 0)
            : 0;

        return new BacktestResult
        {
            Trades = trades,
            EquityCurve = equityCurve,
            StartingBalance = startingBalance,
            FinalBalance = balance,
            WinRatePercent = winRate,
            AverageRiskReward = avgRR,
            MaxDrawdownPercent = maxDrawdown
        };
    }
}