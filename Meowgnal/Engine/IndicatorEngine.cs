using System;
using System.Collections.Generic;
using System.Linq;
using FacioQuo.Stock.Indicators;
using Meowgnal.Models;
using Bar = Meowgnal.Models.Bar;

namespace Meowgnal.Engine;

public static class IndicatorEngine
{
    public static double?[] Calculate(IReadOnlyList<Bar> bars, IndicatorDefinition def)
    {
        int Period(string key, int fallback) =>
            def.Params.TryGetValue(key, out var v) ? (int)v : fallback;

        return def.Type.ToUpperInvariant() switch
        {
            "EMA" => bars.ToEma(Period("period", 14)).Select(r => r.Ema).ToArray(),
            "SMA" => bars.ToSma(Period("period", 14)).Select(r => r.Sma).ToArray(),
            "RSI" => bars.ToRsi(Period("period", 14)).Select(r => r.Rsi).ToArray(),
            "ATR" => bars.ToAtr(Period("period", 14)).Select(r => r.Atr).ToArray(),
            "MACD" => bars.ToMacd(
                            Period("fastPeriods", 12),
                            Period("slowPeriods", 26),
                            Period("signalPeriods", 9))
                          .Select(r => r.Macd).ToArray(),
            _ => throw new NotSupportedException($"Indicator type '{def.Type}' not supported yet.")
        };
    }
}