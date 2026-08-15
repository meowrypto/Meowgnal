using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Meowgnal.Services;

namespace Meowgnal.DataProviders;

// Fetches open interest history from Binance Futures and Hyperliquid.
// Binance: /futures/data/openInterestHist (public, no key, 5m/1h/1d intervals).
// Hyperliquid: current snapshot via /info with type="metaAndAssetCtxs".
public sealed class OpenInterestProvider
{
    private static readonly HttpClient BinanceHttp = new()
    {
        BaseAddress = new Uri("https://fapi.binance.com"),
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static readonly HttpClient HyperHttp = new()
    {
        BaseAddress = new Uri("https://api.hyperliquid.xyz"),
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static readonly FundamentalCache<List<(DateTime Time, double Value)>> HistoryCache =
        new(TimeSpan.FromMinutes(30));

    private static readonly FundamentalCache<double> CurrentCache =
        new(TimeSpan.FromMinutes(5));

    // Binance Futures: historical open interest (default: 1h interval, 500 rows).
    public async Task<List<(DateTime Time, double Value)>> GetBinanceHistoryAsync(string symbol, string period = "1h", int limit = 500)
    {
        var key = $"binance_oi_{symbol}_{period}_{limit}";
        if (HistoryCache.TryGet(key, out var cached) && cached is not null)
            return cached;

        try
        {
            var rawSymbol = symbol.Replace("/", "").ToUpperInvariant();
            var url = $"/futures/data/openInterestHist?symbol={rawSymbol}&period={period}&limit={Math.Min(limit, 500)}";

            await using var stream = await BinanceHttp.GetStreamAsync(url);
            using var doc = await JsonDocument.ParseAsync(stream);

            var result = new List<(DateTime, double)>();
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var ts = row.GetProperty("timestamp").GetInt64();
                var oi = double.Parse(row.GetProperty("sumOpenInterestValue").GetString()!, CultureInfo.InvariantCulture);
                result.Add((DateTimeOffset.FromUnixTimeMilliseconds(ts).UtcDateTime, oi));
            }

            HistoryCache.Set(key, result);
            return result;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"OpenInterestProvider (Binance {symbol}) failed", ex);
            return new List<(DateTime, double)>();
        }
    }

    // Hyperliquid: only the current snapshot is publicly available (no history).
    // Returns OI in USD for the given coin, or null on failure.
    public async Task<double?> GetHyperliquidCurrentAsync(string symbol)
    {
        var coin = symbol.Split('/')[0].ToUpperInvariant();
        if (CurrentCache.TryGet($"hyper_oi_{coin}", out var cached))
            return cached;

        try
        {
            var response = await HyperHttp.PostAsJsonAsync("/info", new { type = "metaAndAssetCtxs" });
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

            if (!doc.RootElement.TryGetProperty("universe", out var universe)) return null;
            if (!doc.RootElement.TryGetProperty("assetCtxs", out var ctxs)) return null;

            var idx = -1;
            for (int i = 0; i < universe.GetArrayLength(); i++)
            {
                if (universe[i].TryGetProperty("name", out var nameEl) &&
                    string.Equals(nameEl.GetString(), coin, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }
            if (idx < 0 || idx >= ctxs.GetArrayLength()) return null;

            var ctx = ctxs[idx];
            if (!ctx.TryGetProperty("openInterest", out var oiEl)) return null;
            if (!ctx.TryGetProperty("markPx", out var pxEl)) return null;

            var oi = oiEl.GetDouble();
            var px = double.Parse(pxEl.GetString()!, CultureInfo.InvariantCulture);
            var oiUsd = oi * px;

            CurrentCache.Set($"hyper_oi_{coin}", oiUsd);
            return oiUsd;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"OpenInterestProvider (Hyperliquid {symbol}) failed", ex);
            return null;
        }
    }

    // Routes to the right provider. For Hyperliquid, returns a 1-point list
    // (current snapshot) because no history is available.
    public async Task<List<(DateTime Time, double Value)>> GetHistoryAsync(string source, string symbol, int limit = 500)
    {
        if (source == "hyperliquid")
        {
            var current = await GetHyperliquidCurrentAsync(symbol);
            return current.HasValue
                ? new List<(DateTime, double)> { (DateTime.UtcNow, current.Value) }
                : new List<(DateTime, double)>();
        }
        return await GetBinanceHistoryAsync(symbol, "1h", limit);
    }
}