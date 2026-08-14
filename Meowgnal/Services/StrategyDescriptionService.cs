using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Meowgnal.Models;

namespace Meowgnal.Services;

// Builds a plain-English, multi-line description of a strategy so that
// non-professional traders can understand exactly what it does.
public static class StrategyDescriptionService
{
    public static string Describe(StrategyDefinition strategy)
    {
        if (strategy is null) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine(DescribeGroup(strategy.EntryRules, strategy, "Entry"));
        sb.AppendLine(DescribeGroup(strategy.ExitRules, strategy, "Exit"));

        var rm = strategy.RiskManagement;
        if (rm is not null)
        {
            var stopMethod = rm.StopLoss?.Method == "ATR" ? "ATR multiple" : "fixed percent";
            var stopValue = Fmt(rm.StopLoss?.Multiplier ?? 2);
            var targetMethod = rm.Target?.Method == "fixedPercent" ? "fixed percent" : "risk to reward ratio";
            var targetValue = Fmt(rm.Target?.Value ?? 2);
            var risk = Fmt(rm.PositionSizing?.RiskPercentPerTrade ?? 1);
            sb.AppendLine($"Stop loss: {stopMethod} ({stopValue}x). Target: {targetMethod} ({targetValue}). Risk per trade: {risk}% of account.");
        }

        return sb.ToString().TrimEnd();
    }

    private static string DescribeGroup(RuleGroup group, StrategyDefinition s, string title)
    {
        if (group is null) return $"{title}: (none)";

        var leaves = group.Conditions?.OfType<LeafCondition>().ToList();
        if (leaves is null || leaves.Count == 0) return $"{title}: (no conditions)";

        var joiner = group.Mode == "any" ? " OR " : group.Mode == "all" ? " AND " : ", ";
        var text = $"{title}: " + string.Join(joiner, leaves.Select(c => Phrase(TokenLabel(c.Left, s), c.Op, RightLabel(c.Right, s))));

        if (group.Mode == "threshold" && group.MinScore is not null)
            text += $" — at least {Fmt(group.MinScore.Value)} points must match (weighted).";

        return text;
    }

    private static string Phrase(string left, string op, string right) => op switch
    {
        "crossesAbove" => $"{left} crosses above {right}",
        "crossesBelow" => $"{left} crosses below {right}",
        "greaterThan" or "above" => $"{left} is greater than {right}",
        "lessThan" or "below" => $"{left} is less than {right}",
        _ => $"{left} {op} {right}"
    };

    // Turns a raw indicator id like "ema9" into a readable label like "EMA(9)".
    private static string TokenLabel(string token, StrategyDefinition s)
    {
        if (string.IsNullOrEmpty(token)) return "?";
        if (token == "price") return "price";
        if (token == "volume") return "volume";
        if (token == "signal") return "MACD signal";

        var ind = s.Indicators?.FirstOrDefault(i => i.Id == token);
        if (ind is null) return token;
        if (ind.Type == "MACD") return "MACD line";

        return $"{ind.Type}({GetPeriod(ind)})";
    }

    private static string RightLabel(object? right, StrategyDefinition s)
    {
        if (right is double d) return Fmt(d);
        return TokenLabel(right?.ToString() ?? "", s);
    }

    private static int GetPeriod(IndicatorDefinition ind)
    {
        var fallback = ind.Type == "MACD" ? 12 : 14;
        if (ind.Params is null) return fallback;
        return ind.Params.TryGetValue("period", out var p) ? ToInt(p, fallback) : fallback;
    }

    // Safely converts a parameter value (int/long/double/string/JsonElement) to int.
    private static int ToInt(object? value, int fallback) => value switch
    {
        int i => i,
        long l => (int)l,
        double d => (int)d,
        string s when int.TryParse(s, out var n) => n,
        JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetInt32(),
        _ => fallback
    };

    private static string Fmt(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}