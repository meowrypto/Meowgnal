using System.Collections.Generic;

namespace Meowgnal.Models;

/// <summary>
/// Represents a single Fibonacci level configuration.
/// </summary>
public class FibLevel
{
    public double Ratio { get; set; }
    public bool Enabled { get; set; } = true;
    public string Color { get; set; } = "#2962FF";
    public string Label { get; set; } = "";
}

/// <summary>
/// Provides default Fibonacci level configurations.
/// </summary>
public static class FibonacciDefaults
{
    public static List<FibLevel> GetDefaultRetracementLevels(string defaultColor)
    {
        return new List<FibLevel>
        {
            new FibLevel { Ratio = 0, Enabled = true, Color = defaultColor, Label = "0" },
            new FibLevel { Ratio = 0.236, Enabled = true, Color = defaultColor, Label = "0.236" },
            new FibLevel { Ratio = 0.382, Enabled = true, Color = defaultColor, Label = "0.382" },
            new FibLevel { Ratio = 0.5, Enabled = true, Color = defaultColor, Label = "0.5" },
            new FibLevel { Ratio = 0.618, Enabled = true, Color = defaultColor, Label = "0.618" },
            new FibLevel { Ratio = 0.786, Enabled = true, Color = defaultColor, Label = "0.786" },
            new FibLevel { Ratio = 1, Enabled = true, Color = defaultColor, Label = "1" }
        };
    }

    public static List<FibLevel> GetDefaultExtensionLevels(string defaultColor)
    {
        return new List<FibLevel>
        {
            new FibLevel { Ratio = 0, Enabled = true, Color = defaultColor, Label = "0" },
            new FibLevel { Ratio = 0.382, Enabled = true, Color = defaultColor, Label = "0.382" },
            new FibLevel { Ratio = 0.618, Enabled = true, Color = defaultColor, Label = "0.618" },
            new FibLevel { Ratio = 1, Enabled = true, Color = defaultColor, Label = "1" },
            new FibLevel { Ratio = 1.272, Enabled = true, Color = defaultColor, Label = "1.272" },
            new FibLevel { Ratio = 1.618, Enabled = true, Color = defaultColor, Label = "1.618" },
            new FibLevel { Ratio = 2.618, Enabled = true, Color = defaultColor, Label = "2.618" },
            new FibLevel { Ratio = 3.618, Enabled = true, Color = defaultColor, Label = "3.618" },
            new FibLevel { Ratio = 4.236, Enabled = true, Color = defaultColor, Label = "4.236" }
        };
    }
}