using System;
using System.Collections.Generic;

namespace Meowgnal.Models;

/// <summary>Type of drawing tool.</summary>
public enum DrawingKind
{
    HorizontalLine,
    TrendLine,
    Fibonacci,
    Ray,
    ExtendedLine,
    HorizontalRay,
    VerticalLine,
    Crossline,
    InfoLine,
    TrendAngle,
    ParallelChannel,
    RegressionTrend,
    FlatTopBottom,
    DisjointChannel,
    Pitchfork,
    SchiffPitchfork,
    ModifiedSchiffPitchfork,
    InsidePitchfork,
    FibExtension,
    FibTimeZone,
    FibCircles,
    FibSpiral,
    FibArcs,
    FibWedge,
    FibSpeedFan,
    Pitchfan,
    GannBox,
    GannSquare,
    GannFan,
    Rectangle,
    RotatedRectangle,
    Circle,
    Ellipse,
    Triangle,
    Polyline,
    Arc,
    Arrow,
    ArrowMarkUp,
    ArrowMarkDown,
    Brush,
    Highlighter,
    Text,
    Note,
    PriceLabel,
    Pin,
    Flag,
    Sticker
}

/// <summary>Standard Fibonacci retracement levels.</summary>
public static class FibonacciLevels
{
    public static readonly double[] Standard = { 0, 0.236, 0.382, 0.5, 0.618, 0.786, 1 };
}

/// <summary>A single point of a drawing; time-based (Unix UTC) so it appears on all timeframes of the same symbol.</summary>
public sealed class DrawingPoint
{
    public long TimeUnix { get; set; }
    public decimal Price { get; set; }
}

/// <summary>A drawing saved in drawings.dat.</summary>
public sealed class Drawing
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Normalized symbol, e.g., BTCUSDT.</summary>
    public string Symbol { get; set; } = "";

    /// <summary>Exchange data source for this drawing: "binance" or "hyperliquid".</summary>
    public string DataSource { get; set; } = "binance";

    public DrawingKind Kind { get; set; } = DrawingKind.HorizontalLine;

    /// <summary>One point for horizontal line; two points (start/end) for trendline and fibonacci.</summary>
    public List<DrawingPoint> Points { get; set; } = new();

    /// <summary>Hex color with #, e.g., #2962FF.</summary>
    public string Color { get; set; } = "#2962FF";

    /// <summary>Line thickness in pixels (1-4).</summary>
    public int LineWidth { get; set; } = 2;

    /// <summary>Line style: "solid" | "dashed" | "dotted".</summary>
    public string LineStyle { get; set; } = "solid";

    /// <summary>Optional label next to the drawing (e.g., 61.8% or Support).</summary>
    public string Label { get; set; } = "";

    /// <summary>Level 2 alert: Alert on cross — Toast and sound only at the exact moment of price crossing.</summary>
    public bool AlertOnCross { get; set; }

    /// <summary>If true, the drawing was created by the auto support-resistance detector.</summary>
    public bool IsAutoDetected { get; set; }

    /// <summary>If true, the drawing cannot be moved or deleted by the eraser.</summary>
    public bool IsLocked { get; set; }

    /// <summary>If false, the drawing is hidden (but still saved).</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>If set, this drawing belongs to a group. All drawings with the same GroupId move and delete together.</summary>
    public string? GroupId { get; set; }

    /// <summary>Z-index for layering. Higher values render on top.</summary>
    public int ZIndex { get; set; } = 0;

    /// <summary>Font size for Text/Note/Sticker drawings (in pixels).</summary>
    public int FontSize { get; set; } = 13;

    /// <summary>Font family for Text/Note/Sticker drawings.</summary>
    public string FontFamily { get; set; } = "Trebuchet MS";

    /// <summary>Custom Gann Fan ratios. If null, defaults [0.25, 0.5, 1, 2, 4] are used.</summary>
    public List<double>? GannRatios { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Root object for drawings.dat (encrypted with DPAPI).</summary>
public sealed class DrawingsFile
{
    public List<Drawing> Drawings { get; set; } = new();
    /// <summary>User's preferred color for new drawings (saved).</summary>
    public string DefaultColor { get; set; } = "#2962FF";
}