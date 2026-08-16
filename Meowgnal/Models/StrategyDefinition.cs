using System.Text.Json.Serialization;

namespace Meowgnal.Models;

public sealed class StrategyDefinition
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("strategyId")]
    public string StrategyId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("market")]
    public string Market { get; set; } = "crypto";

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = "";

    [JsonPropertyName("timeframe")]
    public string Timeframe { get; set; } = "1h";

    [JsonPropertyName("dataSource")]
    public string DataSource { get; set; } = "binance";

    [JsonPropertyName("indicators")]
    public List<IndicatorDefinition> Indicators { get; set; } = new();

    [JsonPropertyName("entryRules")]
    public RuleGroup EntryRules { get; set; } = new();

    [JsonPropertyName("exitRules")]
    public RuleGroup ExitRules { get; set; } = new();

    [JsonPropertyName("riskManagement")]
    public RiskManagementConfig RiskManagement { get; set; } = new();

    [JsonPropertyName("notifications")]
    public NotificationsConfig Notifications { get; set; } = new();

    // Custom sort order for drag & drop in Strategy Manager (0 = first).
    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; } = 0;

    // Pre-Hunt Checklist: null means "use the global default from AppSettings".
    [JsonPropertyName("customChecklist")]
    public List<ChecklistItem>? CustomChecklist { get; set; }
}

public sealed class IndicatorDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    // e.g. "EMA", "SMA", "MACD", "RSI", "ATR" — must match a FacioQuo indicator name
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("params")]
    public Dictionary<string, double> Params { get; set; } = new();
}

public sealed class RuleGroup
{
    // "all" (AND) | "any" (OR) | "threshold" (weighted confluence scoring)
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "all";

    // Only used when Mode == "threshold": sum of matched condition weights must reach this.
    [JsonPropertyName("minScore")]
    public double? MinScore { get; set; }

    // "onTransition" (fire only when the rule turns from false to true)
    // "everyCandle" (fire on every candle where the rule is true)
    [JsonPropertyName("triggerMode")]
    public string TriggerMode { get; set; } = "onTransition";

    [JsonPropertyName("conditions")]
    public List<ConditionNode> Conditions { get; set; } = new();
}

public sealed class RiskManagementConfig
{
    [JsonPropertyName("stopLoss")]
    public StopLossConfig StopLoss { get; set; } = new();

    [JsonPropertyName("target")]
    public TargetConfig Target { get; set; } = new();

    [JsonPropertyName("positionSizing")]
    public PositionSizingConfig PositionSizing { get; set; } = new();
}

public sealed class StopLossConfig
{
    // "ATR" | "swingHighLow" | "fixedPercent"
    [JsonPropertyName("method")]
    public string Method { get; set; } = "ATR";

    [JsonPropertyName("multiplier")]
    public double Multiplier { get; set; } = 1.5;
}

public sealed class TargetConfig
{
    // "riskRewardRatio" | "fixedPercent"
    [JsonPropertyName("method")]
    public string Method { get; set; } = "riskRewardRatio";

    [JsonPropertyName("value")]
    public double Value { get; set; } = 2;
}

public sealed class PositionSizingConfig
{
    [JsonPropertyName("riskPercentPerTrade")]
    public double RiskPercentPerTrade { get; set; } = 1;
}

public sealed class NotificationsConfig
{
    [JsonPropertyName("channels")]
    public List<string> Channels { get; set; } = new() { "toast", "sound" };
    
}