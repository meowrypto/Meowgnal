using System;
using System.Collections.Generic;
using System.Linq;
using FacioQuo.Stock.Indicators;
using Meowgnal.Models;
using Bar = Meowgnal.Models.Bar;

namespace Meowgnal.Engine;

/// <summary>Calculates indicator values from a list of bars.</summary>
public static class IndicatorEngine
{
    /// <summary>
    /// Calculates a single-output indicator (legacy, still used by most indicators).
    /// </summary>
    public static double?[] Calculate(IReadOnlyList<Bar> bars, IndicatorDefinition def)
    {
        int Period(string key, int fallback) =>
            def.Params.TryGetValue(key, out var v) ? ToInt(v, fallback) : fallback;

        return def.Type.ToUpperInvariant() switch
        {
            "EMA" => bars.ToEma(Period("period", 14)).Select(r => r.Ema).ToArray(),
            "SMA" => bars.ToSma(Period("period", 14)).Select(r => r.Sma).ToArray(),
            "RSI" => bars.ToRsi(Period("period", 14)).Select(r => r.Rsi).ToArray(),
            "ATR" => bars.ToAtr(Period("period", 14)).Select(r => r.Atr).ToArray(),
            "STOCH" => bars.ToStoch(Period("period", 14)).Select(r => r.K).ToArray(),
            "ADX" => bars.ToAdx(Period("period", 14)).Select(r => r.Adx).ToArray(),
            "VOLSMA" => CalcVolumeSma(bars, Period("period", 20)),
            "VWAP" => CalcVwap(bars),
            // Multi-output indicators are handled by CalculateMulti — caller should use that.
            "BBANDS" => throw new InvalidOperationException("BBANDS has multiple outputs; use CalculateMulti."),
            "MACD" => bars.ToMacd(
                            Period("fastPeriods", 12),
                            Period("slowPeriods", 26),
                            Period("signalPeriods", 9))
                          .Select(r => r.Macd).ToArray(),
            _ => throw new NotSupportedException($"Indicator type '{def.Type}' not supported yet.")
        };
    }

    /// <summary>
    /// Calculates every output series of an indicator.
    /// For single-output indicators, returns one entry keyed by def.Id.
    /// For multi-output indicators (e.g. BBANDS), returns one entry per sub-output
    /// keyed as "{id}.{subOutput}" (e.g. "bb1.upper", "bb1.middle", "bb1.lower").
    /// </summary>
    public static Dictionary<string, double?[]> CalculateMulti(
        IReadOnlyList<Bar> bars, IndicatorDefinition def)
    {
        var result = new Dictionary<string, double?[]>();

        if (def.Type.Equals("BBANDS", StringComparison.OrdinalIgnoreCase))
        {
            int Period(string key, int fallback) =>
                def.Params.TryGetValue(key, out var v) ? ToInt(v, fallback) : fallback;

            var period = Period("period", 20);
            var stdDev = 2.0;
            var bb = bars.ToBollingerBands(period, stdDev).ToList();

            var upper = new double?[bars.Count];
            var middle = new double?[bars.Count];
            var lower = new double?[bars.Count];

            for (var i = 0; i < bars.Count && i < bb.Count; i++)
            {
                upper[i] = bb[i].UpperBand;
                middle[i] = bb[i].Sma;
                lower[i] = bb[i].LowerBand;
            }

            result[$"{def.Id}.upper"] = upper;
            result[$"{def.Id}.middle"] = middle;
            result[$"{def.Id}.lower"] = lower;
            return result;
        }

        // All other indicators fall back to single-output.
        result[def.Id] = Calculate(bars, def);
        return result;
    }

    // Average of the last N bars' volumes.
    private static double?[] CalcVolumeSma(IReadOnlyList<Bar> bars, int period)
    {
        var result = new double?[bars.Count];
        if (period <= 0) return result;

        var window = new Queue<double>();
        double sum = 0;

        for (var i = 0; i < bars.Count; i++)
        {
            var vol = (double)bars[i].Volume;
            window.Enqueue(vol);
            sum += vol;

            if (window.Count > period) sum -= window.Dequeue();

            result[i] = window.Count >= period ? sum / window.Count : null;
        }
        return result;
    }

    // VWAP resets at the start of each session. For non-intraday timeframes
    // we treat the entire visible range as a single session — a reasonable
    // approximation that still gives a volume-weighted price anchor.
    private static double?[] CalcVwap(IReadOnlyList<Bar> bars)
    {
        var result = new double?[bars.Count];
        double cumPV = 0;
        double cumV = 0;

        for (var i = 0; i < bars.Count; i++)
        {
            var b = bars[i];
            var typical = (double)(b.High + b.Low + b.Close) / 3.0;
            var vol = (double)b.Volume;
            cumPV += typical * vol;
            cumV += vol;
            result[i] = cumV > 0 ? cumPV / cumV : null;
        }
        return result;
    }

    private static int ToInt(object? value, int fallback) => value switch
    {
        int i => i,
        long l => (int)l,
        double d => (int)d,
        string s when int.TryParse(s, out var n) => n,
        System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number => je.GetInt32(),
        _ => fallback
    };
}