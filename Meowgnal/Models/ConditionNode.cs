using System.Text.Json.Serialization;

namespace Meowgnal.Models;

// Base class for a rule tree node.
// Phase 1 UI only creates LeafCondition nodes, but the model already
// supports ConditionGroup so Phase 2 (nested AND/OR groups) needs no
// changes here — only the UI and validation layer change later.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(LeafCondition), "condition")]
[JsonDerivedType(typeof(ConditionGroup), "group")]
public abstract class ConditionNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
}

// A single comparison, e.g. "ema9 crossesAbove ema21" or "rsi14 lessThan 70".
public sealed class LeafCondition : ConditionNode
{
    [JsonPropertyName("left")]
    public string Left { get; set; } = "";

    // crossesAbove | crossesBelow | greaterThan | lessThan | above | below | nearSupport | nearResistance ...
    [JsonPropertyName("op")]
    public string Op { get; set; } = "";

    // Can be a number (e.g. 70) or another indicator id (e.g. "ema21").
    [JsonPropertyName("right")]
    public object Right { get; set; } = 0;

    // Used only when the parent group's Mode is "threshold".
    [JsonPropertyName("weight")]
    public double Weight { get; set; } = 1;

    // Percent distance threshold for nearSupport / nearResistance.
    // E.g. 0.5 means the close must be within 0.5% of the nearest support/resistance level.
    [JsonPropertyName("tolerancePercent")]
    public double TolerancePercent { get; set; } = 0.5;
}

// A sub-group of conditions (reserved for Phase 2 nested AND/OR logic).
public sealed class ConditionGroup : ConditionNode
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "all"; // all | any | threshold

    [JsonPropertyName("minScore")]
    public double? MinScore { get; set; }

    [JsonPropertyName("conditions")]
    public List<ConditionNode> Conditions { get; set; } = new();
}