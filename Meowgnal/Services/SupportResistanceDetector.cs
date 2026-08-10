using System;
using System.Collections.Generic;
using System.Linq;
using Meowgnal.Models;

namespace Meowgnal.Services;

/// <summary>
/// Detects important support and resistance levels automatically based on pivot points and ATR.
/// </summary>
public static class SupportResistanceDetector
{
    private const int LookbackBars = 300;
    private const int PivotLeft = 5;
    private const int PivotRight = 5;
    private const int MinTouches = 3;
    private const decimal TolerancePercent = 0.25m;
    private const int MaxLinesAbove = 3;
    private const int MaxLinesBelow = 3;
    private const int AtrPeriod = 14;

    private class LevelCluster
    {
        public decimal Price { get; set; }
        public int Touches { get; set; }
        public bool IsResistance { get; set; }
    }

    public static List<Drawing> Detect(string symbol, List<Bar> bars)
    {
        var result = new List<Drawing>();
        if (bars == null || bars.Count < LookbackBars) return result;

        var subset = bars.TakeLast(LookbackBars).ToList();
        var currentPrice = subset[^1].Close;

        // Dynamic tolerance based on ATR(14), computed locally with Wilder's smoothing
        var lastAtr = ComputeAtr(subset, AtrPeriod);
        var dynamicTolerance = lastAtr / 2m;

        // Find pivot highs and lows
        var pivots = new List<(int Index, decimal Price, bool IsHigh)>();

        for (int i = PivotLeft; i < subset.Count - PivotRight; i++)
        {
            bool isHigh = true;
            bool isLow = true;

            for (int j = 1; j <= PivotLeft; j++)
            {
                if (subset[i].High <= subset[i - j].High) isHigh = false;
                if (subset[i].Low >= subset[i - j].Low) isLow = false;
            }
            for (int j = 1; j <= PivotRight; j++)
            {
                if (subset[i].High <= subset[i + j].High) isHigh = false;
                if (subset[i].Low >= subset[i + j].Low) isLow = false;
            }

            if (isHigh) pivots.Add((i, subset[i].High, true));
            if (isLow) pivots.Add((i, subset[i].Low, false));
        }

        // Cluster pivots into levels
        var levels = new List<LevelCluster>();

        foreach (var p in pivots)
        {
            var tol = Math.Max(dynamicTolerance, p.Price * (TolerancePercent / 100m));

            var existing = levels.FirstOrDefault(l => Math.Abs(l.Price - p.Price) <= tol);
            if (existing != null)
            {
                existing.Price = (existing.Price * existing.Touches + p.Price) / (existing.Touches + 1);
                existing.Touches++;
                existing.IsResistance = existing.Price > currentPrice;
            }
            else
            {
                levels.Add(new LevelCluster { Price = p.Price, Touches = 1, IsResistance = p.Price > currentPrice });
            }
        }

        // Filter by min touches
        levels = levels.Where(l => l.Touches >= MinTouches).ToList();

        // Separate above and below, sort by touches, then by distance to current price
        var above = levels.Where(l => l.Price > currentPrice)
                          .OrderByDescending(l => l.Touches)
                          .ThenBy(l => l.Price - currentPrice)
                          .Take(MaxLinesAbove)
                          .ToList();

        var below = levels.Where(l => l.Price < currentPrice)
                          .OrderByDescending(l => l.Touches)
                          .ThenByDescending(l => currentPrice - l.Price)
                          .Take(MaxLinesBelow)
                          .ToList();

        var selected = above.Concat(below);
        var cleanSymbol = symbol.Replace("/", "");

        foreach (var level in selected)
        {
            result.Add(new Drawing
            {
                Symbol = cleanSymbol,
                Kind = DrawingKind.HorizontalLine,
                Color = level.IsResistance ? "#F23645" : "#089981",
                Label = level.IsResistance ? "Resistance" : "Support",
                AlertOnCross = false,
                IsAutoDetected = true,
                Points = new List<DrawingPoint>
                {
                    new DrawingPoint { TimeUnix = new DateTimeOffset(subset[0].Timestamp).ToUnixTimeSeconds(), Price = level.Price },
                    new DrawingPoint { TimeUnix = new DateTimeOffset(subset[^1].Timestamp).ToUnixTimeSeconds(), Price = level.Price }
                }
            });
        }

        return result;
    }

    /// <summary>
    /// Computes ATR (Average True Range) using Wilder's smoothing.
    /// </summary>
    private static decimal ComputeAtr(List<Bar> bars, int period)
    {
        if (bars.Count < period + 1) return 0m;

        var trueRanges = new List<decimal>(bars.Count - 1);
        for (int i = 1; i < bars.Count; i++)
        {
            var high = bars[i].High;
            var low = bars[i].Low;
            var prevClose = bars[i - 1].Close;

            var tr = Math.Max(high - low, Math.Max(Math.Abs(high - prevClose), Math.Abs(low - prevClose)));
            trueRanges.Add(tr);
        }

        if (trueRanges.Count < period) return 0m;

        var atr = trueRanges.Take(period).Average();
        for (int i = period; i < trueRanges.Count; i++)
            atr = (atr * (period - 1) + trueRanges[i]) / period;

        return atr;
    }
}