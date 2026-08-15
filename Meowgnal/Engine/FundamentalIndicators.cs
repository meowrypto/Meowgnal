using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Meowgnal.DataProviders;
using Meowgnal.Models;

namespace Meowgnal.Engine;

// Converts raw fundamental API data into time-series arrays
// aligned with the chart's candle timestamps (so they can be
// plotted and used in backtests just like technical indicators).
public static class FundamentalIndicators
{
    private static readonly FearGreedProvider FearGreed = new();
    private static readonly MarketDominanceProvider MarketDom = new();
    private static readonly FundingRateProvider Funding = new();
    private static readonly OpenInterestProvider OpenInterest = new();

    public static async Task<double?[]> GetSeriesAsync(
        string type, IReadOnlyList<Bar> bars, string source, string symbol)
    {
        if (bars.Count == 0) return Array.Empty<double?>();

        try
        {
            return type.ToUpperInvariant() switch
            {
                "FEARGREED" => await BuildFearGreedSeries(bars),
                "BTCDOM" => await BuildBtcDomSeries(bars),
                "FUNDING" => await BuildFundingSeries(bars, source, symbol),
                "OI" => await BuildOiSeries(bars, source, symbol),
                _ => new double?[bars.Count]
            };
        }
        catch
        {
            return new double?[bars.Count];
        }
    }

    // Forward-fills daily Fear & Greed onto each bar's date.
    private static async Task<double?[]> BuildFearGreedSeries(IReadOnlyList<Bar> bars)
    {
        var history = await FearGreed.GetHistoryAsync();
        if (history.Count == 0) return new double?[bars.Count];

        var byDate = history.ToDictionary(h => h.Date, h => h.Value);
        var result = new double?[bars.Count];

        double? lastKnown = null;
        for (int i = 0; i < bars.Count; i++)
        {
            var barDate = bars[i].Timestamp.Date;
            if (byDate.TryGetValue(barDate, out var v)) lastKnown = v;
            result[i] = lastKnown;
        }
        return result;
    }

    // BTC Dominance has no history — broadcast the current value across all bars.
    private static async Task<double?[]> BuildBtcDomSeries(IReadOnlyList<Bar> bars)
    {
        var current = await MarketDom.GetBtcDominanceAsync();
        if (!current.HasValue) return new double?[bars.Count];
        var result = new double?[bars.Count];
        for (int i = 0; i < bars.Count; i++) result[i] = current.Value;
        return result;
    }

    // Aligns Binance/Hyperliquid funding-rate history to each bar by latest prior time.
    private static async Task<double?[]> BuildFundingSeries(
        IReadOnlyList<Bar> bars, string source, string symbol)
    {
        var history = await Funding.GetHistoryAsync(source, symbol, 1000);
        if (history.Count == 0) return new double?[bars.Count];

        var sorted = history.OrderBy(h => h.Time).ToList();
        var result = new double?[bars.Count];
        int j = 0;
        double? lastKnown = null;
        for (int i = 0; i < bars.Count; i++)
        {
            while (j < sorted.Count && sorted[j].Time <= bars[i].Timestamp)
            {
                lastKnown = sorted[j].Rate;
                j++;
            }
            result[i] = lastKnown;
        }
        return result;
    }

    // Aligns open-interest history (or Hyperliquid's single snapshot) to each bar.
    private static async Task<double?[]> BuildOiSeries(
        IReadOnlyList<Bar> bars, string source, string symbol)
    {
        var history = await OpenInterest.GetHistoryAsync(source, symbol, 500);
        if (history.Count == 0) return new double?[bars.Count];

        var sorted = history.OrderBy(h => h.Time).ToList();
        var result = new double?[bars.Count];
        int j = 0;
        double? lastKnown = null;
        for (int i = 0; i < bars.Count; i++)
        {
            while (j < sorted.Count && sorted[j].Time <= bars[i].Timestamp)
            {
                lastKnown = sorted[j].Value;
                j++;
            }
            result[i] = lastKnown;
        }
        return result;
    }
}