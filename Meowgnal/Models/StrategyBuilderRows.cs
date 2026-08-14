namespace Meowgnal.Models;

// Lightweight row objects the Strategy Builder UI binds to directly.
public sealed class IndicatorRow
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "EMA";
    public int Period { get; set; } = 14;
}

public sealed class ConditionRow
{
    public string Left { get; set; } = "";
    public string Op { get; set; } = "crossesAbove";
    public string Right { get; set; } = "";
    public double Weight { get; set; } = 1;
    public double Tolerance { get; set; } = 0.5;
}