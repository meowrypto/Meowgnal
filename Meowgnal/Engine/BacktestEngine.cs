using FacioQuo.Stock.Indicators;
using Meowgnal.Models;
using Meowgnal.Services;
using Meowgnal.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using static Meowgnal.Models.BacktestResult;
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
        Dictionary<string, decimal> _pendingEntrySnapshot = new();
        string _pendingEntryExplanation = "";

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

                // Capture real indicator values at the entry bar for autopsy.
                var entrySnapshot = RuleEngine.CaptureSnapshot(
                    strategy.EntryRules.Conditions, i, bars, series);
                _pendingEntrySnapshot = entrySnapshot;
                _pendingEntryExplanation = StrategyDescriptionService.DescribeTradeEntry(strategy, entrySnapshot);

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

                    // Build exit autopsy.
                    var reasonEnum = exitReason switch
                    {
                        "stopLoss" => CloseReason.StopLoss,
                        "target" => CloseReason.TakeProfit,
                        "signal" => CloseReason.SignalExit,
                        _ => CloseReason.SignalExit
                    };
                    var exitSnapshot = exitReason == "signal"
                        ? RuleEngine.CaptureSnapshot(strategy.ExitRules.Conditions, i, bars, series)
                        : new Dictionary<string, decimal>();
                    var exitExplanation = StrategyDescriptionService.DescribeTradeExit(
                        reasonEnum, exitPrice, strategy, exitSnapshot);

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
                        PnLPercent = (double)((exitPrice - entryPrice) / entryPrice * 100m),
                        IndicatorSnapshotAtEntry = _pendingEntrySnapshot,
                        EntryExplanation = _pendingEntryExplanation,
                        ExitExplanation = exitExplanation
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
            MaxDrawdownPercent = maxDrawdown,
            SharpeRatio = CalculateSharpeRatio(trades),
            SortinoRatio = CalculateSortinoRatio(trades),
            MonthlyBreakdown = BuildMonthlyBreakdown(trades),
            SampleSizeWarning = ComputeSampleSizeWarning(trades.Count)
        };
    }

    // Runs the normal backtest, then splits its trades by the market regime
    // that was active when each trade was entered.
    public static RegimeBacktestResult RunByRegime(
        StrategyDefinition strategy, IReadOnlyList<Bar> bars,
        decimal startingBalance, decimal feePercent, decimal slippagePercent)
    {
        var overall = Run(strategy, bars, startingBalance, feePercent, slippagePercent);
        var periods = MarketRegimeDetector.Detect(bars);

        var result = new RegimeBacktestResult { Overall = overall, HasRegimeData = periods.Count > 0 };
        if (!result.HasRegimeData) return result;

        var bull = new List<BacktestTrade>();
        var bear = new List<BacktestTrade>();
        var sideways = new List<BacktestTrade>();

        foreach (var trade in overall.Trades)
        {
            switch (MarketRegimeDetector.RegimeAt(periods, trade.EntryTime))
            {
                case MarketRegimeDetector.Bull: bull.Add(trade); break;
                case MarketRegimeDetector.Bear: bear.Add(trade); break;
                default: sideways.Add(trade); break;
            }
        }

        // Self-check: the three buckets must cover all trades.
        var bucketed = bull.Count + bear.Count + sideways.Count;
        if (bucketed != overall.Trades.Count)
            AppLogger.Error($"Regime bucket mismatch: {bucketed} bucketed vs {overall.Trades.Count} total trades.");

        result.BullMarket = AggregateTrades(bull, startingBalance);
        result.BearMarket = AggregateTrades(bear, startingBalance);
        result.SidewaysMarket = AggregateTrades(sideways, startingBalance);
        return result;
    }

    public static WalkForwardResult RunWalkForward(
        StrategyDefinition strategy,
        IReadOnlyList<Bar> bars,
        decimal startingBalance,
        decimal feePercent,
        decimal slippagePercent,
        int windows,
        double oosPercent)
    {
        var result = new WalkForwardResult();
        if (bars.Count < windows * 20) return result;

        var chunkSize = bars.Count / windows;
        var allIsTrades = new List<BacktestTrade>();
        var allOosTrades = new List<BacktestTrade>();

        for (var w = 0; w < windows; w++)
        {
            var startIdx = w * chunkSize;
            var endIdx = (w == windows - 1) ? bars.Count : (w + 1) * chunkSize;

            var chunkBars = bars.Skip(startIdx).Take(endIdx - startIdx).ToList();
            var oosSize = (int)(chunkBars.Count * (oosPercent / 100.0));
            var isSize = chunkBars.Count - oosSize;

            if (isSize < 20 || oosSize < 10) continue;

            var isBars = chunkBars.Take(isSize).ToList();
            var oosBars = chunkBars.Skip(isSize).ToList();

            var isResult = Run(strategy, isBars, startingBalance, feePercent, slippagePercent);
            var oosResult = Run(strategy, oosBars, startingBalance, feePercent, slippagePercent);

            result.InSampleResults.Add(isResult);
            result.OutOfSampleResults.Add(oosResult);

            allIsTrades.AddRange(isResult.Trades);
            allOosTrades.AddRange(oosResult.Trades);
        }

        result.AggregateInSample = AggregateTrades(allIsTrades, startingBalance);
        result.AggregateOutOfSample = AggregateTrades(allOosTrades, startingBalance);

        // Overfit detection heuristic
        if (result.AggregateInSample.Trades.Count > 0 && result.AggregateOutOfSample.Trades.Count > 0)
        {
            var isWinRate = result.AggregateInSample.WinRatePercent;
            var oosWinRate = result.AggregateOutOfSample.WinRatePercent;
            var isPf = CalculateProfitFactor(allIsTrades);
            var oosPf = CalculateProfitFactor(allOosTrades);

            // Flag as overfit if OOS win rate drops >20% compared to IS, or IS is profitable but OOS is not
            if (isWinRate > 0 && (oosWinRate < isWinRate * 0.8 || (isPf > 1.0 && oosPf < 1.0)))
            {
                result.IsOverfit = true;
                result.OverfitReason = $"OOS Win Rate ({oosWinRate:F1}%) dropped significantly compared to IS ({isWinRate:F1}%).";
            }
        }

        return result;
    }

    private static BacktestResult AggregateTrades(List<BacktestTrade> trades, decimal startingBalance)
    {
        if (trades.Count == 0)
            return new BacktestResult { StartingBalance = startingBalance, FinalBalance = startingBalance };

        var wins = trades.Count(t => t.PnL > 0);
        var winRate = (double)wins / trades.Count * 100;
        var avgRR = trades.Average(t => Math.Abs(t.EntryPrice - t.StopLossPrice) != 0
            ? (double)(Math.Abs(t.ExitPrice - t.EntryPrice) / Math.Abs(t.EntryPrice - t.StopLossPrice))
            : 0);

        var currentBal = startingBalance;
        var peak = startingBalance;
        var maxDd = 0.0;
        var equityCurve = new List<(DateTime, decimal)>();

        foreach (var t in trades.OrderBy(x => x.ExitTime))
        {
            currentBal += t.PnL;
            peak = Math.Max(peak, currentBal);
            var dd = peak > 0 ? (double)((peak - currentBal) / peak * 100m) : 0;
            maxDd = Math.Max(maxDd, dd);
            equityCurve.Add((t.ExitTime, currentBal));
        }

        return new BacktestResult
        {
            Trades = trades,
            EquityCurve = equityCurve,
            StartingBalance = startingBalance,
            FinalBalance = currentBal,
            WinRatePercent = winRate,
            AverageRiskReward = avgRR,
            MaxDrawdownPercent = maxDd,
            SharpeRatio = CalculateSharpeRatio(trades),
            SortinoRatio = CalculateSortinoRatio(trades),
            MonthlyBreakdown = BuildMonthlyBreakdown(trades),
            SampleSizeWarning = ComputeSampleSizeWarning(trades.Count)
        };
    }

    private static double CalculateProfitFactor(List<BacktestTrade> trades)
    {
        var grossProfit = trades.Where(t => t.PnL > 0).Sum(t => t.PnL);
        var grossLoss = Math.Abs(trades.Where(t => t.PnL < 0).Sum(t => t.PnL));
        return grossLoss > 0 ? (double)(grossProfit / grossLoss) : (grossProfit > 0 ? 999 : 0);
    }

    // Sharpe ratio: reward per unit of total risk (higher is better)
    private static double CalculateSharpeRatio(List<BacktestTrade> trades)
    {
        if (trades.Count < 2) return 0;
        var returns = trades.Select(t => t.PnLPercent).ToArray();
        var mean = returns.Average();
        var variance = returns.Sum(r => (r - mean) * (r - mean)) / (returns.Length - 1);
        var stdDev = Math.Sqrt(variance);
        if (stdDev <= 0) return 0;
        return mean / stdDev * Math.Sqrt(returns.Length);
    }

    // Sortino ratio: like Sharpe but only counts downside (losing) risk
    private static double CalculateSortinoRatio(List<BacktestTrade> trades)
    {
        if (trades.Count < 2) return 0;
        var returns = trades.Select(t => t.PnLPercent).ToArray();
        var mean = returns.Average();
        var downsideDeviation = Math.Sqrt(returns.Select(r => r < 0 ? r * r : 0).Average());
        if (downsideDeviation <= 0) return mean > 0 ? 99 : 0; // capped: no losing trades
        return mean / downsideDeviation * Math.Sqrt(returns.Length);
    }

    // Group trades by calendar month for the monthly performance table
    private static List<MonthlyPerformance> BuildMonthlyBreakdown(List<BacktestTrade> trades)
    {
        return trades
            .GroupBy(t => new DateTime(t.ExitTime.Year, t.ExitTime.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => new MonthlyPerformance
            {
                MonthLabel = g.Key.ToString("yyyy-MM"),
                TradeCount = g.Count(),
                NetPnL = g.Sum(t => t.PnL),
                WinRatePercent = (double)g.Count(t => t.PnL > 0) / g.Count() * 100
            })
            .ToList();
    }
    // Standard statistical thresholds for sample-size reliability.
    private static string ComputeSampleSizeWarning(int tradeCount) => tradeCount switch
    {
        < 30 => "low",
        < 100 => "moderate",
        _ => "reliable"
    };
}