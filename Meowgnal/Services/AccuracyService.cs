using Meowgnal.Engine;
using Meowgnal.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Meowgnal.Services;

/// <summary>Phase 34 — Accuracy Engine: lightweight filters that cut false signals.</summary>
public static class AccuracyService
{
    /// <summary>True if the signal fired on the still-forming (last) candle.</summary>
    public static bool IsFormingCandleSignal(SignalEvent signal, List<Bar> bars) =>
        bars.Count > 0 && signal.Timestamp == bars[^1].Timestamp;

    public static double[] Ema(double[] values, int period)
    {
        var result = new double[values.Length];
        if (values.Length == 0) return result;
        var k = 2.0 / (period + 1);
        result[0] = values[0];
        for (var i = 1; i < values.Length; i++)
            result[i] = values[i] * k + result[i - 1] * (1 - k);
        return result;
    }

    /// <summary>Higher-timeframe trend gate: EMA20 vs EMA50 on the HTF (last closed candle).</summary>
    public static bool HtfTrendOk(List<Bar> htfBars, SignalType direction)
    {
        if (htfBars == null || htfBars.Count < 60) return true;
        var closes = htfBars.Select(b => (double)b.Close).ToArray();
        var ema20 = Ema(closes, 20);
        var ema50 = Ema(closes, 50);
        var i = closes.Length - 2;
        var up = ema20[i] >= ema50[i];
        return direction == SignalType.Entry ? up : true;
    }

    /// <summary>Volume gate: last closed candle volume vs 20-period average.</summary>
    public static bool VolumeOk(List<Bar> bars, double multiplier)
    {
        if (bars == null || bars.Count < 25) return true;
        var vols = bars.Select(b => (double)b.Volume).ToArray();
        var last = vols[^2];
        var avg = vols.Skip(vols.Length - 22).Take(20).Average();
        return avg <= 0 || last >= avg * multiplier;
    }

    /// <summary>Regime gate: block entries when volatility is in the lowest 20% (choppy market).</summary>
    public static bool RegimeOk(List<Bar> bars)
    {
        if (bars == null || bars.Count < 40) return true;
        var atr = AtrPercentSeries(bars);
        if (atr.Length < 30) return true;
        var current = atr[^2];
        var sorted = atr.OrderBy(x => x).ToArray();
        var rank = Array.BinarySearch(sorted, current);
        if (rank < 0) rank = ~rank;
        return rank >= sorted.Length * 0.2;
    }

    private static double[] AtrPercentSeries(List<Bar> bars)
    {
        var n = bars.Count;
        var result = new double[n];
        double atr = 0;
        for (var i = 1; i < n; i++)
        {
            var tr = Math.Max(
                (double)bars[i].High - (double)bars[i].Low,
                Math.Max(Math.Abs((double)bars[i].High - (double)bars[i - 1].Close),
                         Math.Abs((double)bars[i].Low - (double)bars[i - 1].Close)));
            atr = i == 1 ? tr : (atr * 13 + tr) / 14.0;
            result[i] = (double)bars[i].Close > 0 ? atr / (double)bars[i].Close : 0;
        }
        return result;
    }

    /// <summary>Maps a timeframe to the next higher one (x4).</summary>
    public static string? NextHtf(string tf) => tf switch
    {
        "1m" or "5m" or "15m" => "1h",
        "30m" or "1h" => "4h",
        "2h" or "4h" => "1d",
        "1d" => "1w",
        _ => null
    };
}