using Meowgnal.Models;

namespace Meowgnal.Services;

/// <summary>
/// Calculates pitchfork geometry for all 4 variants.
/// Used as reference implementation; actual rendering is in chart.html (pixel space).
/// </summary>
public static class PitchforkCalculator
{
    /// <summary>Result of pitchfork calculation in price/time space.</summary>
    public sealed class PitchforkResult
    {
        /// <summary>Anchor point where the median line starts.</summary>
        public DrawingPoint MedianStart { get; set; } = new();
        /// <summary>Midpoint of P2-P3 (median line passes through here).</summary>
        public DrawingPoint MedianEnd { get; set; } = new();
        /// <summary>Point that arm 1 passes through (P2 for standard, modified for inside).</summary>
        public DrawingPoint Arm1Point { get; set; } = new();
        /// <summary>Point that arm 2 passes through (P3 for standard, modified for inside).</summary>
        public DrawingPoint Arm2Point { get; set; } = new();
        /// <summary>Slope of the median line in price-per-time units.</summary>
        public double Slope { get; set; }
    }

    /// <summary>
    /// Calculates pitchfork geometry for the given variant.
    /// </summary>
    /// <param name="p1">First anchor point (handle).</param>
    /// <param name="p2">Second anchor point (first tine).</param>
    /// <param name="p3">Third anchor point (second tine).</param>
    /// <param name="variant">One of: "Standard", "Schiff", "ModifiedSchiff", "Inside".</param>
    public static PitchforkResult Calculate(DrawingPoint p1, DrawingPoint p2, DrawingPoint p3, string variant)
    {
        var midP2P3Time = (p2.TimeUnix + p3.TimeUnix) / 2.0;
        var midP2P3Price = (double)(p2.Price + p3.Price) / 2.0;

        double anchorTime, anchorPrice;

        switch (variant)
        {
            case "Schiff":
                // Median starts at midpoint of P1-P2
                anchorTime = (p1.TimeUnix + p2.TimeUnix) / 2.0;
                anchorPrice = (double)(p1.Price + p2.Price) / 2.0;
                break;
            case "ModifiedSchiff":
                // Median starts at midpoint between P1 and midpoint(P1, P2)
                // = (3*P1 + P2) / 4
                anchorTime = (3.0 * p1.TimeUnix + p2.TimeUnix) / 4.0;
                anchorPrice = (3.0 * (double)p1.Price + (double)p2.Price) / 4.0;
                break;
            case "Inside":
            case "Standard":
            default:
                // Median starts at P1
                anchorTime = p1.TimeUnix;
                anchorPrice = (double)p1.Price;
                break;
        }

        // Calculate slope of median line
        var deltaTime = midP2P3Time - anchorTime;
        var slope = deltaTime != 0 ? (midP2P3Price - anchorPrice) / deltaTime : 0;

        // Calculate arm points
        DrawingPoint arm1Point, arm2Point;

        if (variant == "Inside")
        {
            // Arms pass through midpoints between median line and P2/P3
            var medianAtP2 = anchorPrice + slope * (p2.TimeUnix - anchorTime);
            var medianAtP3 = anchorPrice + slope * (p3.TimeUnix - anchorTime);
            arm1Point = new DrawingPoint
            {
                TimeUnix = p2.TimeUnix,
                Price = (decimal)((medianAtP2 + (double)p2.Price) / 2.0)
            };
            arm2Point = new DrawingPoint
            {
                TimeUnix = p3.TimeUnix,
                Price = (decimal)((medianAtP3 + (double)p3.Price) / 2.0)
            };
        }
        else
        {
            // Standard/Schiff/ModifiedSchiff: arms pass through P2 and P3
            arm1Point = new DrawingPoint { TimeUnix = p2.TimeUnix, Price = p2.Price };
            arm2Point = new DrawingPoint { TimeUnix = p3.TimeUnix, Price = p3.Price };
        }

        return new PitchforkResult
        {
            MedianStart = new DrawingPoint { TimeUnix = (long)anchorTime, Price = (decimal)anchorPrice },
            MedianEnd = new DrawingPoint { TimeUnix = (long)midP2P3Time, Price = (decimal)midP2P3Price },
            Arm1Point = arm1Point,
            Arm2Point = arm2Point,
            Slope = slope
        };
    }
}