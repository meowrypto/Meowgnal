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

    public DrawingKind Kind { get; set; } = DrawingKind.HorizontalLine;

    /// <summary>One point for horizontal line; two points (start/end) for trendline and fibonacci.</summary>
    public List<DrawingPoint> Points { get; set; } = new();

    /// <summary>Hex color with #, e.g., #2962FF.</summary>
    public string Color { get; set; } = "#2962FF";

    /// <summary>Optional label next to the drawing (e.g., 61.8% or Support).</summary>
    public string Label { get; set; } = "";

    /// <summary>Level 2 alert: Alert on cross — Toast and sound only at the exact moment of price crossing.</summary>
    public bool AlertOnCross { get; set; }

    /// <summary>If true, the drawing was created by the auto support-resistance detector.</summary>
    public bool IsAutoDetected { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Root object for drawings.dat (encrypted with DPAPI).</summary>
public sealed class DrawingsFile
{
    public List<Drawing> Drawings { get; set; } = new();
}