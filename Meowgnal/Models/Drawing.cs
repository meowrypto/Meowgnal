using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

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
    FibChannel,
    FibTimeZone,
    TrendBasedFibTime,
    FibCircles,
    FibSpiral,
    FibArcs,
    FibWedge,
    FibSpeedFan,
    Pitchfan,
    GannBox,
    GannSquare,
    GannSquareFixed,
    GannFan,
    XabcdPattern,
    CypherPattern,
    HeadAndShoulders,
    AbcdPattern,
    TrianglePattern,
    ThreeDrivesPattern,
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
    Sticker,
    ElliottImpulseWave,
    ElliottCorrectionWave,
    ElliottTriangleWave,
    ElliottDoubleComboWave,
    ElliottTripleComboWave,
    CyclicLines,
    TimeCycles,
    SineLine,
    LongPosition,
    ShortPosition,
    PositionForecast,
    BarsPattern,
    GhostFeed,
    Sector,
    ArrowMarker,
    Path,
    Curve,
    DoubleCurve,
    AnchoredVwap,
    FixedRangeVolumeProfile,
    AnchoredVolumeProfile,
    PriceRange,
    DateRange,
    DateAndPriceRange,
    AnchoredText,
    AnchoredNote,
    Callout,
    Comment,
    PriceNote,
    Signpost,
    FlagMark,
    Table
}

/// <summary>A single point of a drawing; time-based (Unix UTC) so it appears on all timeframes of the same symbol.</summary>
public sealed class DrawingPoint
{
    public long TimeUnix { get; set; }
    public decimal Price { get; set; }
}

/// <summary>A drawing saved in drawings.dat (encrypted with DPAPI).</summary>
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
    /// <summary>Custom Gann Fan ratios. If null, defaults [0.125, 0.25, 0.333, 0.5, 1, 2, 3, 4, 8] are used.</summary>
    public List<double>? GannRatios { get; set; }
    /// <summary>Number of grid divisions for Gann Square tools (default 4).</summary>
    public int GannSquareDivisions { get; set; } = 4;

    // ----- Line-tool display options (TradingView-style) -----
    /// <summary>Trendline: extend the segment beyond the left anchor.</summary>
    public bool ExtendLeft { get; set; }
    /// <summary>Trendline: extend the segment beyond the right anchor.</summary>
    public bool ExtendRight { get; set; }
    /// <summary>Trendline / Horizontal / HorizontalRay / Crossline: show price label(s).</summary>
    public bool ShowPriceLabels { get; set; } = true;
    /// <summary>VerticalLine / Crossline: show date-time label.</summary>
    public bool ShowTimeLabel { get; set; } = true;
    /// <summary>InfoLine: include percent change in the info label.</summary>
    public bool ShowPriceChange { get; set; } = true;
    /// <summary>InfoLine: include bar count in the info label.</summary>
    public bool ShowBarCount { get; set; }
    /// <summary>InfoLine: include elapsed time in the info label.</summary>
    public bool ShowTimeElapsed { get; set; }
    /// <summary>InfoLine / TrendAngle: include slope angle in the info label.</summary>
    public bool ShowAngle { get; set; }

    // ----- Fibonacci settings -----
    /// <summary>Custom Fibonacci levels for this drawing. If empty, defaults are used based on the tool kind.</summary>
    public List<FibLevel> FibLevels { get; set; } = new();

    // ----- Channel settings -----
    /// <summary>Fill the area between channel lines with color.</summary>
    public bool FillBackground { get; set; }
    /// <summary>Opacity of the channel fill (0.0 - 1.0).</summary>
    public double FillOpacity { get; set; } = 0.15;
    /// <summary>ParallelChannel: show the median (middle) line.</summary>
    public bool ShowMedianLine { get; set; }
    /// <summary>ParallelChannel: color of the median line.</summary>
    public string MedianLineColor { get; set; } = "#FF9800";
    /// <summary>ParallelChannel: style of the median line.</summary>
    public string MedianLineStyle { get; set; } = "dashed";
    /// <summary>RegressionTrend: number of standard deviations for channel width (1-3).</summary>
    public int StdDevMultiplier { get; set; } = 2;
    /// <summary>DisjointChannel: color of the second line (empty = use main color).</summary>
    public string SecondLineColor { get; set; } = "";

    // ----- Pitchfork settings -----
    /// <summary>Pitchfork: if true, all 3 lines use the same Color. If false, each line has its own color.</summary>
    public bool PitchforkUseSameColor { get; set; } = true;
    /// <summary>Pitchfork: color of the median line (used when PitchforkUseSameColor is false).</summary>
    public string PitchforkMedianColor { get; set; } = "#FF9800";
    /// <summary>Pitchfork: color of arm 1 / P2 side (used when PitchforkUseSameColor is false).</summary>
    public string PitchforkArm1Color { get; set; } = "#2962FF";
    /// <summary>Pitchfork: color of arm 2 / P3 side (used when PitchforkUseSameColor is false).</summary>
    public string PitchforkArm2Color { get; set; } = "#2962FF";

    // ----- Pattern settings -----
    /// <summary>Show Fibonacci ratios on pattern legs.</summary>
    public bool ShowRatios { get; set; } = true;
    /// <summary>Head and Shoulders: Neckline color.</summary>
    public string NecklineColor { get; set; } = "#FF9800";
    /// <summary>Show labels (Head/Shoulders, Drive numbers, etc.).</summary>
    public bool ShowLabels { get; set; } = true;
    /// <summary>Optional separate color for pattern/Elliott labels. Empty means use main line color.</summary>
    public string LabelColor { get; set; } = "";

    // ----- Forecasting & Measurement settings -----
    /// <summary>Position side: "long" or "short".</summary>
    public string PositionSide { get; set; } = "long";
    /// <summary>Long/Short position: entry price.</summary>
    public decimal EntryPrice { get; set; }
    /// <summary>Long/Short position: stop loss price.</summary>
    public decimal StopLossPrice { get; set; }
    /// <summary>Long/Short position: take profit price.</summary>
    public decimal TakeProfitPrice { get; set; }
    /// <summary>Position size as percentage of account (0-100).</summary>
    public decimal PositionSizePercent { get; set; } = 10;
    /// <summary>Color for profit zone (default green).</summary>
    public string ProfitZoneColor { get; set; } = "#089981";
    /// <summary>Color for loss zone (default red).</summary>
    public string LossZoneColor { get; set; } = "#F23645";
    /// <summary>Ghost feed: symbol to overlay.</summary>
    public string GhostSymbol { get; set; } = "";
    /// <summary>Ghost feed: data source (binance/hyperliquid).</summary>
    public string GhostDataSource { get; set; } = "binance";
    /// <summary>Ghost feed: cached candles for overlay (not serialized).</summary>
    [JsonIgnore]
    public List<Bar> GhostCandles { get; set; } = new();
    /// <summary>Ghost feed opacity (0-1).</summary>
    public double GhostOpacity { get; set; } = 0.5;
    /// <summary>Bars pattern: opacity (0-1).</summary>
    public double BarsPatternOpacity { get; set; } = 0.5;
    /// <summary>Sector: fill opacity (0-1).</summary>
    public double SectorFillOpacity { get; set; } = 0.3;

    // ----- Volume-based & measurer tool settings -----
    /// <summary>Anchored VWAP: show +/-1 and +/-2 standard deviation bands.</summary>
    public bool ShowVwapBands { get; set; } = true;
    /// <summary>Volume profile: number of price buckets (default 24).</summary>
    public int VolumeBucketCount { get; set; } = 24;
    /// <summary>Volume profile: max bar width as percent of chart width.</summary>
    public double VolumeProfileWidthPercent { get; set; } = 25;
    /// <summary>Volume profile: bar color.</summary>
    public string VolumeProfileColor { get; set; } = "#2962FF";
    /// <summary>Price range tool: "both" | "absolute" | "percent".</summary>
    public string PriceRangeMode { get; set; } = "both";
    /// <summary>Date range tool: "days" | "hours" | "bars".</summary>
    public string DateRangeUnit { get; set; } = "days";

    // ----- Brushes & Shapes settings -----
    /// <summary>Arrow head style: "open" | "solid" | "stealth".</summary>
    public string ArrowHeadStyle { get; set; } = "open";
    /// <summary>Arrow Marker direction: "up" | "down" | "left" | "right".</summary>
    public string ArrowMarkerDirection { get; set; } = "up";

    /// <summary>Triangle pattern: Show apex point and dashed lines.</summary>
    public bool ShowApex { get; set; } = true;

    // ----- Cycles settings -----
    /// <summary>Cycles: number of vertical lines to display (default 10).</summary>
    public int CycleCount { get; set; } = 10;
    /// <summary>Cycles: interval in seconds between cycle lines. If 0, calculated from points.</summary>
    public long CycleIntervalSeconds { get; set; }
    /// <summary>Sine line: amplitude as percentage of price distance between two points (default 50).</summary>
    public double SineAmplitudePercent { get; set; } = 50;
    /// <summary>Sine line: number of wave repetitions to the right (default 3).</summary>
    public int SineRepeatCount { get; set; } = 3;

    // ----- Text & Notes settings -----
    /// <summary>Text/Note/Callout/Comment: bold font weight.</summary>
    public bool IsBold { get; set; }
    /// <summary>Text/Note/Callout/Comment: italic font style.</summary>
    public bool IsItalic { get; set; }
    /// <summary>Text/Note/Callout/Comment: background fill color.</summary>
    public string TextBgColor { get; set; } = "#1E222D";
    /// <summary>Text/Note/Callout/Comment: background fill opacity (0.0 - 1.0).</summary>
    public double TextBgOpacity { get; set; } = 0.8;
    /// <summary>Text/Note/Callout/Comment: enable background fill.</summary>
    public bool TextBgEnabled { get; set; }
    /// <summary>Text/Note/Callout/Comment: border color around the text box.</summary>
    public string TextBorderColor { get; set; } = "#2A2E39";
    /// <summary>Text/Note/Callout/Comment: enable border around the text box.</summary>
    public bool TextBorderEnabled { get; set; }

    // ----- Anchored tools settings -----
    /// <summary>Anchored tools (AnchoredText/AnchoredNote/Signpost/FlagMark/PriceNote): X pixel offset from anchor point.</summary>
    public double AnchoredPixelX { get; set; }
    /// <summary>Anchored tools (AnchoredText/AnchoredNote/Signpost/FlagMark/PriceNote): Y pixel offset from anchor point.</summary>
    public double AnchoredPixelY { get; set; }

    // ----- Table settings -----
    /// <summary>Table: number of rows (default 3).</summary>
    public int TableRows { get; set; } = 3;
    /// <summary>Table: number of columns (default 3).</summary>
    public int TableCols { get; set; } = 3;
    /// <summary>Table: background color of cells.</summary>
    public string TableBgColor { get; set; } = "#1E222D";
    /// <summary>Table: border color of cells.</summary>
    public string TableBorderColor { get; set; } = "#2A2E39";
    /// <summary>Table: cell contents serialized as JSON string of List<List<string>>.</summary>
    public string TableData { get; set; } = "[]";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Root object for drawings.dat (encrypted with DPAPI).</summary>
public sealed class DrawingsFile
{
    public List<Drawing> Drawings { get; set; } = new();
    /// <summary>User's preferred color for new drawings (saved).</summary>
    public string DefaultColor { get; set; } = "#2962FF";
}