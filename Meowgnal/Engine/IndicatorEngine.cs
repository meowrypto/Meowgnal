using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FacioQuo.Stock.Indicators;
using Meowgnal.Models;
using Meowgnal.Services;
using Bar = Meowgnal.Models.Bar;

namespace Meowgnal.Engine;

public static class IndicatorEngine
{
    // Cache filled by PrefetchFundamentalsAsync before each scan / chart redraw.
    private static readonly Dictionary<string, double?[]> _fundamentalCache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _cacheLock = new();

    // Fetches every fundamental indicator referenced by the given definitions,
    // aligned to the candle timeline. Called before backtest / chart rendering.
    public static async Task PrefetchFundamentalsAsync(
        IReadOnlyList<Bar> bars,
        IEnumerable<IndicatorDefinition> defs,
        string source,
        string symbol)
    {
        var targets = new List<IndicatorDefinition>();
        foreach (var def in defs)
        {
            var info = IndicatorRegistry.All.FirstOrDefault(i =>
                string.Equals(i.Type, def.Type, StringComparison.OrdinalIgnoreCase));
            if (info is not null && info.IsFundamental) targets.Add(def);
        }
        if (targets.Count == 0) return;

        var tasks = targets.Select(def => Task.Run(async () =>
        {
            try
            {
                var series = await FundamentalIndicators.GetSeriesAsync(
                    def.Type, bars, source, symbol);
                lock (_cacheLock) { _fundamentalCache[def.Id] = series; }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Prefetch fundamental {def.Type} failed", ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks);
    }

    public static void ClearFundamentalCache()
    {
        lock (_cacheLock) { _fundamentalCache.Clear(); }
    }

    public static double?[] Calculate(IReadOnlyList<Bar> bars, IndicatorDefinition def)
    {
        int Period(string key, int fallback) =>
            def.Params.TryGetValue(key, out var v) ? ToInt(v, fallback) : fallback;

        var type = def.Type.ToUpperInvariant();

        // Multi-output indicators must use CalculateMulti
        if (type == "BBANDS" || type == "KELTNER" || type == "DONCHIAN" || type == "AROON" ||
            type == "ICHIMOKU" || type == "VORTEX")
            throw new InvalidOperationException($"{def.Type} has multiple outputs; use CalculateMulti.");

        // Fundamental indicators are served from the prefetch cache
        if (type is "FEARGREED" or "BTCDOM" or "FUNDING" or "OI")
            throw new InvalidOperationException($"{def.Type} is fundamental; use CalculateMulti after PrefetchFundamentalsAsync.");

        return type switch
        {
            "SMA" => bars.ToSma(Period("period", 20)).Select(r => r.Sma).ToArray(),
            "EMA" => bars.ToEma(Period("period", 9)).Select(r => r.Ema).ToArray(),
            "WMA" => bars.ToWma(Period("period", 20)).Select(r => r.Wma).ToArray(),
            "HMA" => bars.ToHma(Period("period", 20)).Select(r => r.Hma).ToArray(),
            "DEMA" => bars.ToDema(Period("period", 20)).Select(r => r.Dema).ToArray(),
            "TEMA" => bars.ToTema(Period("period", 20)).Select(r => r.Tema).ToArray(),
            "KAMA" => bars.ToKama(Period("period", 10)).Select(r => r.Kama).ToArray(),
            "VWMA" => bars.ToVwma(Period("period", 20)).Select(r => r.Vwma).ToArray(),

            "RSI" => bars.ToRsi(Period("period", 14)).Select(r => r.Rsi).ToArray(),
            "STOCH" => bars.ToStoch(Period("period", 14)).Select(r => r.K).ToArray(),
            "STOCHRSI" => bars.ToStochRsi(Period("period", 14)).Select(r => r.StochRsi).ToArray(),
            "CCI" => bars.ToCci(Period("period", 20)).Select(r => r.Cci).ToArray(),
            "WILLIAMSR" => bars.ToWilliamsR(Period("period", 14)).Select(r => r.WilliamsR).ToArray(),
            "MFI" => bars.ToMfi(Period("period", 14)).Select(r => r.Mfi).ToArray(),
            "ROC" => bars.ToRoc(Period("period", 10)).Select(r => r.Roc).ToArray(),
            "TRIX" => bars.ToTrix(Period("period", 15)).Select(r => r.Trix).ToArray(),
            "ULTIMATE" => bars.ToUltimate(Period("period", 28)).Select(r => r.Ultimate).ToArray(),
            "AO" => bars.ToAwesome().Select(r => r.Oscillator).ToArray(),
            "CMO" => bars.ToCmo(Period("period", 14)).Select(r => r.Cmo).ToArray(),
            "CONNORSRSI" => bars.ToConnorsRsi(Period("period", 14)).Select(r => r.ConnorsRsi).ToArray(),
            "MACD" => bars.ToMacd(Period("fastPeriods", 12), Period("slowPeriods", 26), Period("signalPeriods", 9)).Select(r => r.Macd).ToArray(),
            "ADX" => bars.ToAdx(Period("period", 14)).Select(r => r.Adx).ToArray(),

            "ATR" => bars.ToAtr(Period("period", 14)).Select(r => r.Atr).ToArray(),
            "STDDEV" => bars.ToStdDev(Period("period", 20)).Select(r => r.StdDev).ToArray(),
            "ULCER" => bars.ToUlcerIndex(Period("period", 14)).Select(r => r.UlcerIndex).ToArray(),

            "VOLSMA" => CalcVolumeSma(bars, Period("period", 20)),
            "VWAP" => CalcVwap(bars),
            "OBV" => bars.ToObv().Select(r => (double?)r.Obv).ToArray(),
            "CMF" => bars.ToCmf(Period("period", 20)).Select(r => r.Cmf).ToArray(),
            "FORCEINDEX" => bars.ToForceIndex(Period("period", 13)).Select(r => (double?)r.ForceIndex).ToArray(),
            "ADL" => bars.ToAdl().Select(r => (double?)r.Adl).ToArray(),

            "SAR" => bars.ToParabolicSar(0.02, 0.2).Select(r => (double?)r.Sar).ToArray(),
            "SUPERTREND" => bars.ToSuperTrend(Period("period", 10), 3.0).Select(r => (double?)r.SuperTrend).ToArray(),
            "CHOP" => bars.ToChop(Period("period", 14)).Select(r => r.Chop).ToArray(),

            _ => throw new NotSupportedException($"Indicator type '{def.Type}' not supported yet.")
        };
    }

    public static Dictionary<string, double?[]> CalculateMulti(
        IReadOnlyList<Bar> bars, IndicatorDefinition def)
    {
        var result = new Dictionary<string, double?[]>();

        int Period(string key, int fallback) =>
            def.Params.TryGetValue(key, out var v) ? ToInt(v, fallback) : fallback;

        var type = def.Type.ToUpperInvariant();

        // Fundamental indicators: serve from the prefetch cache.
        if (type is "FEARGREED" or "BTCDOM" or "FUNDING" or "OI")
        {
            lock (_cacheLock)
            {
                if (_fundamentalCache.TryGetValue(def.Id, out var cached))
                {
                    result[def.Id] = cached;
                    return result;
                }
            }
            result[def.Id] = new double?[bars.Count];
            return result;
        }

        if (type == "BBANDS")
        {
            var bb = bars.ToBollingerBands(Period("period", 20), 2.0).ToList();
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

        if (type == "KELTNER")
        {
            var keltner = bars.ToKeltner(Period("period", 20), 2.0).ToList();
            var upper = new double?[bars.Count];
            var middle = new double?[bars.Count];
            var lower = new double?[bars.Count];
            for (var i = 0; i < bars.Count && i < keltner.Count; i++)
            {
                upper[i] = (double?)keltner[i].UpperBand;
                middle[i] = (double?)keltner[i].Centerline;
                lower[i] = (double?)keltner[i].LowerBand;
            }
            result[$"{def.Id}.upper"] = upper;
            result[$"{def.Id}.middle"] = middle;
            result[$"{def.Id}.lower"] = lower;
            return result;
        }

        if (type == "DONCHIAN")
        {
            var donchian = bars.ToDonchian(Period("period", 20)).ToList();
            var upper = new double?[bars.Count];
            var middle = new double?[bars.Count];
            var lower = new double?[bars.Count];
            for (var i = 0; i < bars.Count && i < donchian.Count; i++)
            {
                upper[i] = (double?)donchian[i].UpperBand;
                middle[i] = (double?)donchian[i].Centerline;
                lower[i] = (double?)donchian[i].LowerBand;
            }
            result[$"{def.Id}.upper"] = upper;
            result[$"{def.Id}.middle"] = middle;
            result[$"{def.Id}.lower"] = lower;
            return result;
        }

        if (type == "AROON")
        {
            var aroon = bars.ToAroon(Period("period", 25)).ToList();
            var up = new double?[bars.Count];
            var down = new double?[bars.Count];
            for (var i = 0; i < bars.Count && i < aroon.Count; i++)
            {
                up[i] = (double?)aroon[i].AroonUp;
                down[i] = (double?)aroon[i].AroonDown;
            }
            result[$"{def.Id}.up"] = up;
            result[$"{def.Id}.down"] = down;
            return result;
        }

        if (type == "ICHIMOKU")
        {
            var ichimoku = bars.ToIchimoku(9, 26, 52).ToList();
            var tenkan = new double?[bars.Count];
            var kijun = new double?[bars.Count];
            var senkouA = new double?[bars.Count];
            var senkouB = new double?[bars.Count];
            var chikou = new double?[bars.Count];
            for (var i = 0; i < bars.Count && i < ichimoku.Count; i++)
            {
                tenkan[i] = (double?)ichimoku[i].TenkanSen;
                kijun[i] = (double?)ichimoku[i].KijunSen;
                senkouA[i] = (double?)ichimoku[i].SenkouSpanA;
                senkouB[i] = (double?)ichimoku[i].SenkouSpanB;
                chikou[i] = (double?)ichimoku[i].ChikouSpan;
            }
            result[$"{def.Id}.tenkan"] = tenkan;
            result[$"{def.Id}.kijun"] = kijun;
            result[$"{def.Id}.senkouA"] = senkouA;
            result[$"{def.Id}.senkouB"] = senkouB;
            result[$"{def.Id}.chikou"] = chikou;
            return result;
        }

        if (type == "VORTEX")
        {
            var n = Period("period", 14);
            var plus = new double?[bars.Count];
            var minus = new double?[bars.Count];

            if (bars.Count >= 2 && n > 0)
            {
                var plusVm = new double[bars.Count];
                var minusVm = new double[bars.Count];
                var tr = new double[bars.Count];

                for (var i = 1; i < bars.Count; i++)
                {
                    var h = (double)bars[i].High;
                    var l = (double)bars[i].Low;
                    var ph = (double)bars[i - 1].High;
                    var pl = (double)bars[i - 1].Low;
                    var pc = (double)bars[i - 1].Close;

                    plusVm[i] = Math.Abs(h - pl);
                    minusVm[i] = Math.Abs(l - ph);
                    tr[i] = Math.Max(h - l, Math.Max(Math.Abs(h - pc), Math.Abs(l - pc)));
                }

                double sumPvm = 0, sumMvm = 0, sumTr = 0;
                var start = Math.Max(1, n);
                for (var i = 1; i < start && i < bars.Count; i++)
                {
                    sumPvm += plusVm[i];
                    sumMvm += minusVm[i];
                    sumTr += tr[i];
                }

                for (var i = 1; i < bars.Count; i++)
                {
                    if (i > n)
                    {
                        sumPvm += plusVm[i] - plusVm[i - n];
                        sumMvm += minusVm[i] - minusVm[i - n];
                        sumTr += tr[i] - tr[i - n];
                    }

                    if (i >= n && sumTr > 0)
                    {
                        plus[i] = sumPvm / sumTr;
                        minus[i] = sumMvm / sumTr;
                    }
                }
            }

            result[$"{def.Id}.plus"] = plus;
            result[$"{def.Id}.minus"] = minus;
            return result;
        }

        result[def.Id] = Calculate(bars, def);
        return result;
    }

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