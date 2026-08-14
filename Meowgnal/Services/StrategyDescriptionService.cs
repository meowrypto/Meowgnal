using System;
using System.Collections.Generic;
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

        sb.AppendLine(DescribeRootGroup(strategy.EntryRules, strategy, "Entry"));
        sb.AppendLine(DescribeRootGroup(strategy.ExitRules, strategy, "Exit"));

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

    private static string DescribeRootGroup(RuleGroup? group, StrategyDefinition strategy, string title)
    {
        if (group is null) return $"{title}: (none)";

        var body = DescribeConditions(group.Conditions, group.Mode, strategy);

        if (string.IsNullOrWhiteSpace(body))
            return $"{title}: (no conditions)";

        var text = $"{title}: {body}";

        if (group.Mode == "threshold" && group.MinScore is not null)
            text += $" — at least {Fmt(group.MinScore.Value)} points must match (weighted).";

        return text;
    }

    // Recursively describes a list of conditions (both LeafCondition and nested ConditionGroup).
    private static string DescribeConditions(
        IEnumerable<ConditionNode>? conditions,
        string mode,
        StrategyDefinition strategy)
    {
        if (conditions is null) return string.Empty;

        var parts = new List<string>();

        foreach (var node in conditions)
        {
            if (node is LeafCondition leaf)
            {
                var phrase = Phrase(
                    TokenLabel(leaf.Left, strategy),
                    leaf.Op,
                    RightLabel(leaf.Right, strategy),
                    leaf);

                if (!string.IsNullOrWhiteSpace(phrase))
                    parts.Add(phrase);
            }
            else if (node is ConditionGroup group)
            {
                var nested = DescribeConditions(group.Conditions, group.Mode, strategy);

                if (!string.IsNullOrWhiteSpace(nested))
                {
                    var groupText = $"({nested})";

                    if (group.Mode == "threshold" && group.MinScore is not null)
                        groupText += $" — at least {Fmt(group.MinScore.Value)} points";

                    parts.Add(groupText);
                }
            }
        }

        if (parts.Count == 0) return string.Empty;
        if (parts.Count == 1) return parts[0];

        return string.Join(GetJoiner(mode), parts);
    }

    private static string GetJoiner(string mode) => mode switch
    {
        "any" => " OR ",
        "all" => " AND ",
        _ => ", "
    };

    private static string Phrase(string left, string op, string right, LeafCondition condition) => op switch
    {
        "crossesAbove" => $"{left} crosses above {right}",
        "crossesBelow" => $"{left} crosses below {right}",
        "greaterThan" or "above" => $"{left} is greater than {right}",
        "lessThan" or "below" => $"{left} is less than {right}",
        "nearSupport" => $"{left} is near support (within {Fmt(condition.TolerancePercent)}%)",
        "nearResistance" => $"{left} is near resistance (within {Fmt(condition.TolerancePercent)}%)",
        _ => $"{left} {op} {right}"
    };

    // Turns a raw token like "ema9" or "bb1.upper" into a readable label.
    private static string TokenLabel(string? token, StrategyDefinition strategy)
    {
        if (string.IsNullOrEmpty(token)) return "?";
        if (token == "price") return "price";
        if (token == "volume") return "volume";
        if (token == "signal") return "MACD signal";

        var dotIndex = token.IndexOf('.');
        if (dotIndex > 0)
        {
            var indicatorId = token.Substring(0, dotIndex);
            var sub = token.Substring(dotIndex + 1);

            var indicator = strategy.Indicators?.FirstOrDefault(i => i.Id == indicatorId);
            if (indicator is not null) return $"{indicator.Type} {sub}";
        }

        var foundIndicator = strategy.Indicators?.FirstOrDefault(i => i.Id == token);
        if (foundIndicator is null) return token;

        if (foundIndicator.Type == "MACD") return "MACD line";

        return $"{foundIndicator.Type}({GetPeriod(foundIndicator)})";
    }

    private static string RightLabel(object? right, StrategyDefinition strategy)
    {
        switch (right)
        {
            case null:
                return string.Empty;

            case double d:
                return Fmt(d);

            case int i:
                return i.ToString(CultureInfo.InvariantCulture);

            case long l:
                return l.ToString(CultureInfo.InvariantCulture);

            case decimal m:
                return m.ToString(CultureInfo.InvariantCulture);

            case bool b:
                return b ? "true" : "false";

            case string text:
                return TokenLabel(text, strategy);

            case JsonElement json:
                return JsonElementLabel(json, strategy);

            default:
                return TokenLabel(right.ToString() ?? string.Empty, strategy);
        }
    }

    private static string JsonElementLabel(JsonElement element, StrategyDefinition strategy)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.TryGetDouble(out var value) ? Fmt(value) : element.GetRawText();

            case JsonValueKind.String:
                return TokenLabel(element.GetString() ?? string.Empty, strategy);

            default:
                return element.GetRawText();
        }
    }

    private static int GetPeriod(IndicatorDefinition indicator)
    {
        var fallback = indicator.Type == "MACD" ? 12 : 14;

        if (indicator.Params is null)
            return fallback;

        return indicator.Params.TryGetValue("period", out var period)
            ? ToInt(period, fallback)
            : fallback;
    }

    private static int ToInt(object? value, int fallback) => value switch
    {
        int i => i,
        long l => (int)l,
        double d => (int)d,
        string s when int.TryParse(s, out var n) => n,
        JsonElement json when json.ValueKind == JsonValueKind.Number => json.GetInt32(),
        _ => fallback
    };

    private static string Fmt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}