namespace Meowgnal.Models;

/// <summary>
/// A pre-built strategy template shown in the Template Store.
/// </summary>
public sealed class StrategyTemplate
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    // Badges for UI display
    public string Style { get; set; } = "Trend";
    public string DefaultTimeframe { get; set; } = "1h";
    public string RiskLevel { get; set; } = "Medium";

    // Factory method to create a StrategyDefinition for a specific symbol
    public System.Func<string, StrategyDefinition>? BuildStrategy { get; set; }
}