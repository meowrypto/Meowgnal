using System;
using System.Collections.Generic;
using Meowgnal.Models;

namespace Meowgnal.Engine;

// Detects market regimes (Bull / Bear / Sideways) algorithmically from the
// candle data itself, using the slope of a long-term EMA over a rolling window.
// This avoids hand-defined date ranges and works for any symbol/timeframe.
public static class MarketRegimeDetector
{
    public const string Bull = "Bull";
    public const string Bear = "Bear";
    public const string Sideways = "Sideways";

    // Rolling window (in candles) used to measure the long-term EMA slope.
    private const int WindowSize = 50;

    // Slope thresholds in percent over the window: above = Bull, below = Bear.
    private const double SlopeThresholdPercent = 0.5;

    public sealed record RegimePeriod(DateTime Start, DateTime End, string Regime);

    // Splits the whole bar range into contiguous labeled regime periods.
    public static List<RegimePeriod> Detect(IReadOnlyList<Bar> bars, int emaPeriod = 200)
    {
        var result = new List<RegimePeriod>();
        if (bars.Count < WindowSize + 10) return result;

        var ema = ComputeEma(bars, emaPeriod);

        var periodStart = bars[0].Timestamp;
        var currentRegime = Sideways;
        var initialized = false;

        for (var i = WindowSize; i < bars.Count; i++)
        {
            var from = ema[i - WindowSize];
            var to = ema[i];
            if (from <= 0m) continue;

            var slopePct = (double)((to - from) / from) * 100.0;
            var regime = slopePct > SlopeThresholdPercent ? Bull
                : slopePct < -SlopeThresholdPercent ? Bear
                : Sideways;

            if (!initialized)
            {
                currentRegime = regime;
                initialized = true;
                continue;
            }

            if (regime != currentRegime)
            {
                result.Add(new RegimePeriod(periodStart, bars[i].Timestamp, currentRegime));
                currentRegime = regime;
                periodStart = bars[i].Timestamp;
            }
        }

        if (initialized)
            result.Add(new RegimePeriod(periodStart, bars[^1].Timestamp, currentRegime));

        return result;
    }

    // Returns the regime label for a given timestamp, or null if unknown.
    public static string? RegimeAt(List<RegimePeriod> periods, DateTime time)
    {
        foreach (var p in periods)
            if (time >= p.Start && time <= p.End) return p.Regime;
        return null;
    }

    // Standard EMA seeded with the first close (warm-up handled implicitly).
    private static decimal[] ComputeEma(IReadOnlyList<Bar> bars, int period)
    {
        var ema = new decimal[bars.Count];
        if (bars.Count == 0) return ema;

        var multiplier = 2m / (period + 1);
        ema[0] = bars[0].Close;
        for (var i = 1; i < bars.Count; i++)
            ema[i] = (bars[i].Close - ema[i - 1]) * multiplier + ema[i - 1];
        return ema;
    }
}