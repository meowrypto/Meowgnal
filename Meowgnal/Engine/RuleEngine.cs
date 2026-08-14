using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Meowgnal.Models;
using Meowgnal.Services;

namespace Meowgnal.Engine;

public enum SignalType { Entry, Exit }

public sealed record SignalEvent(int BarIndex, DateTime Timestamp, SignalType Type);

public static class RuleEngine
{
    // Calculates every indicator defined in the strategy, once, aligned by bar index.
    // Multi-output indicators expand to multiple series (e.g. bb1.upper / middle / lower).
    public static Dictionary<string, double?[]> CalculateIndicatorSeries(
        IReadOnlyList<Bar> bars, List<IndicatorDefinition> indicators)
    {
        var result = new Dictionary<string, double?[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in indicators)
        {
            foreach (var kv in IndicatorEngine.CalculateMulti(bars, def))
                result[kv.Key] = kv.Value;
        }
        return result;
    }

    // Scans every bar and returns the list of entry/exit signal events,
    // respecting each rule group's TriggerMode (onTransition vs everyCandle).
    public static List<SignalEvent> ScanForSignals(StrategyDefinition strategy, IReadOnlyList<Bar> bars)
    {
        var series = CalculateIndicatorSeries(bars, strategy.Indicators);
        var levels = SupportResistanceDetector.FindLevels(bars);
        var events = new List<SignalEvent>();
        bool prevEntry = false, prevExit = false;

        for (var i = 0; i < bars.Count; i++)
        {
            var entryNow = EvaluateRuleGroup(strategy.EntryRules, i, bars, series, levels);
            var exitNow = EvaluateRuleGroup(strategy.ExitRules, i, bars, series, levels);

            var fireEntry = strategy.EntryRules.TriggerMode == "everyCandle" ? entryNow : entryNow && !prevEntry;
            var fireExit = strategy.ExitRules.TriggerMode == "everyCandle" ? exitNow : exitNow && !prevExit;

            if (fireEntry) events.Add(new SignalEvent(i, bars[i].Timestamp, SignalType.Entry));
            if (fireExit) events.Add(new SignalEvent(i, bars[i].Timestamp, SignalType.Exit));

            prevEntry = entryNow;
            prevExit = exitNow;
        }
        return events;
    }

    public static bool EvaluateRuleGroup(
        RuleGroup rule, int index, IReadOnlyList<Bar> bars,
        Dictionary<string, double?[]> series,
        List<(decimal Price, bool IsResistance)> levels)
        => EvaluateGroup(rule.Mode, rule.MinScore, rule.Conditions, index, bars, series, levels);

    // Overload without explicit levels: computes them on demand from the bars.
    // Used by legacy callers (e.g. BacktestEngine) that do not need to share levels across calls.
    public static bool EvaluateRuleGroup(
        RuleGroup rule, int index, IReadOnlyList<Bar> bars,
        Dictionary<string, double?[]> series)
    {
        var levels = SupportResistanceDetector.FindLevels(bars);
        return EvaluateGroup(rule.Mode, rule.MinScore, rule.Conditions, index, bars, series, levels);
    }

    private static bool EvaluateGroup(
        string mode, double? minScore, List<ConditionNode> conditions,
        int index, IReadOnlyList<Bar> bars,
        Dictionary<string, double?[]> series,
        List<(decimal Price, bool IsResistance)> levels)
    {
        if (conditions.Count == 0) return false;

        return mode switch
        {
            "all" => conditions.All(c => EvaluateNode(c, index, bars, series, levels)),
            "any" => conditions.Any(c => EvaluateNode(c, index, bars, series, levels)),
            "threshold" => ScoreConditions(conditions, index, bars, series, levels) >= (minScore ?? 0),
            _ => false
        };
    }

    private static double ScoreConditions(
        List<ConditionNode> conditions, int index, IReadOnlyList<Bar> bars,
        Dictionary<string, double?[]> series,
        List<(decimal Price, bool IsResistance)> levels)
    {
        double score = 0;
        foreach (var node in conditions)
        {
            if (!EvaluateNode(node, index, bars, series, levels)) continue;
            score += node is LeafCondition leaf ? leaf.Weight : 1;
        }
        return score;
    }

    private static bool EvaluateNode(
        ConditionNode node, int index, IReadOnlyList<Bar> bars,
        Dictionary<string, double?[]> series,
        List<(decimal Price, bool IsResistance)> levels)
        => node switch
        {
            LeafCondition leaf => EvaluateLeaf(leaf, index, bars, series, levels),
            ConditionGroup group => EvaluateGroup(group.Mode, group.MinScore, group.Conditions, index, bars, series, levels),
            _ => false
        };

    private static bool EvaluateLeaf(
        LeafCondition c, int index, IReadOnlyList<Bar> bars,
        Dictionary<string, double?[]> series,
        List<(decimal Price, bool IsResistance)> levels)
    {
        // S/R proximity checks operate directly on the bar's close price
        // and the pre-computed level list — no numeric left/right needed.
        if (c.Op == "nearSupport" || c.Op == "nearResistance")
        {
            var close = (double)bars[index].Close;
            var tolerance = Math.Max(0.05, c.TolerancePercent) / 100.0;
            var wantResistance = c.Op == "nearResistance";

            foreach (var (price, isResistance) in levels)
            {
                if (isResistance != wantResistance) continue;
                var distance = Math.Abs(close - (double)price) / Math.Abs(close);
                if (distance <= tolerance) return true;
            }
            return false;
        }

        var left = ResolveValue(c.Left, index, bars, series);
        var right = ResolveRight(c.Right, index, bars, series);
        if (left is null || right is null) return false;

        switch (c.Op)
        {
            case "greaterThan":
            case "above":
                return left > right;
            case "lessThan":
            case "below":
                return left < right;
            case "crossesAbove":
            case "crossesBelow":
                // First bar of the dataset has no prior bar, so it can't "cross" anything.
                if (index == 0) return false;
                var prevLeft = ResolveValue(c.Left, index - 1, bars, series);
                var prevRight = ResolveRight(c.Right, index - 1, bars, series);
                if (prevLeft is null || prevRight is null) return false;
                return c.Op == "crossesAbove"
                    ? prevLeft <= prevRight && left > right
                    : prevLeft >= prevRight && left < right;
            default:
                return false;
        }
    }

    private static double? ResolveValue(
        string reference, int index, IReadOnlyList<Bar> bars, Dictionary<string, double?[]> series)
    {
        if (reference == "price") return (double)bars[index].Close;
        if (reference == "volume") return (double)bars[index].Volume;
        // Supports both plain ids (ema9) and multi-output ids (bb1.upper).
        return series.TryGetValue(reference, out var arr) && index < arr.Length ? arr[index] : null;
    }

    private static double? ResolveRight(
        object right, int index, IReadOnlyList<Bar> bars, Dictionary<string, double?[]> series) => right switch
        {
            double d => d,
            JsonElement { ValueKind: JsonValueKind.Number } je => je.GetDouble(),
            JsonElement { ValueKind: JsonValueKind.String } je => ResolveValue(je.GetString()!, index, bars, series),
            string s => ResolveValue(s, index, bars, series),
            _ => null
        };
}