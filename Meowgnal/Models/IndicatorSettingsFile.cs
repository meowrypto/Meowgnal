using System.Collections.Generic;

namespace Meowgnal.Models;

// Persisted indicator preferences: favorites + active indicators per symbol.
public class IndicatorSettingsFile
{
    public List<string> FavoriteIndicatorTypes { get; set; } = new();
    public Dictionary<string, List<ActiveIndicator>> ActiveIndicators { get; set; } = new();
}

public class ActiveIndicator
{
    public string Type { get; set; } = "";
    public int Period { get; set; }

    // Optional user overrides chosen from the chart hover toolbar (gear icon).
    public string? Color { get; set; }
    public int LineWidth { get; set; } = 2;
}