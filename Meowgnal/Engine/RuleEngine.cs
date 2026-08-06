using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Meowgnal.Models;

namespace Meowgnal.Engine;

public enum SignalType { Entry, Exit }

public sealed record SignalEvent(int BarIndex, DateTime Timestamp, SignalType Type);

public static class RuleEngine
{
    // Calculates every indicator defined in the strategy, once, aligned by bar index.
    public static Dictionary<string, double?[]> CalculateIndicatorSeries(
        IReadOnlyList<Bar> bars, List<IndicatorDefinition> indicators)
    {
        var result = new Dictionary<string, double?[]>();
        foreach (var def in indicators)
            result[def.Id] = IndicatorEngine.Calculate(bars, def);
        return result;
    }

    // Scans every bar and returns the list of entry/exit signal events,
    // respecting each rule group's TriggerMode (onTransition vs everyCandle).
    public static List<SignalEvent> ScanForSignals(StrategyDefinition strategy, IReadOnlyList<Bar> bars)
    {
        var series = CalculateIndicatorSeries(bars, strategy.Indicators);
        var events = new List<SignalEvent>();
        bool prevEntry = false, prevExit = false;

        for (var i = 0; i < bars.Count; i++)
        {
            var entryNow = EvaluateRuleGroup(strategy.EntryRules, i, bars, series);
            var exitNow = EvaluateRuleGroup(strategy.ExitRules, i, bars, series);

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
        RuleGroup rule, int index, IReadOnlyList<Bar> bars, Dictionary<string, double?[]> series)
        => EvaluateGroup(rule.Mode, rule.MinScore, rule.Conditions, index, bars, series);

    private static bool EvaluateGroup(
        string mode, double? minScore, List<ConditionNode> conditions,
        int index, IReadOnlyList<Bar> bars, Dictionary<string, double?[]> series)
    {
        if (conditions.Count == 0) return false;

        return mode switch
        {
            "all" => conditions.All(c => EvaluateNode(c, index, bars, series)),
            "any" => conditions.Any(c => EvaluateNode(c, index, bars, series)),
            "threshold" => ScoreConditions(conditions, index, bars, series) >= (minScore ?? 0),
            _ => false
        };
    }

    private static double ScoreConditions(
        List<ConditionNode> conditions, int index, IReadOnlyList<Bar> bars, Dictionary<string, double?[]> series)
    {
        double score = 0;
        foreach (var node in conditions)
        {
            if (!EvaluateNode(node, index, bars, series)) continue;
            score += node is LeafCondition leaf ? leaf.Weight : 1;
        }
        return score;
    }

    private static bool EvaluateNode(
        ConditionNode node, int index, IReadOnlyList<Bar> bars, Dictionary<string, double?[]> series)
        => node switch
        {
            LeafCondition leaf => EvaluateLeaf(leaf, index, bars, series),
            ConditionGroup group => EvaluateGroup(group.Mode, group.MinScore, group.Conditions, index, bars, series),
            _ => false
        };

    private static bool EvaluateLeaf(
        LeafCondition c, int index, IReadOnlyList<Bar> bars, Dictionary<string, double?[]> series)
    {
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