using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Meowgnal.Models;

namespace Meowgnal.DataProviders;

// Uses Hyperliquid's public info API — no API key needed for market data.
// History is fetched in backward chunks so deep history (EMA200 etc.) works.
public sealed class HyperliquidDataProvider : IDataProvider
{
    private const int MaxBatch = 500;

    public string Name => "hyperliquid";

    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://api.hyperliquid.xyz")
    };

    // 24h change is expensive to compute per coin, so it is cached for 5 min.
    private static readonly Dictionary<string, (double Chg, DateTime FetchedAt)> DailyChangeCache = new();

    // Milliseconds per candle for each supported interval.
    private static readonly Dictionary<string, long> IntervalMs = new()
    {
        ["1m"] = 60_000,
        ["5m"] = 300_000,
        ["15m"] = 900_000,
        ["30m"] = 1_800_000,
        ["1h"] = 3_600_000,
        ["2h"] = 7_200_000,
        ["4h"] = 14_400_000,
        ["8h"] = 28_800_000,
        ["12h"] = 43_200_000,
        ["1d"] = 86_400_000,
        ["3d"] = 259_200_000,
        ["1w"] = 604_800_000,
        ["1M"] = 2_592_000_000,
    };

    public async Task<List<Bar>> GetHistoricalCandlesAsync(string symbol, string timeframe, int limit = 200)
    {
        // Our strategy format uses "BTC/USDT"; Hyperliquid just wants the coin, e.g. "BTC".
        var coin = symbol.Split('/')[0].ToUpperInvariant();
        if (!IntervalMs.TryGetValue(timeframe, out var stepMs))
            throw new ArgumentException($"Unsupported timeframe '{timeframe}' for Hyperliquid.");

        var result = new List<Bar>(limit);
        var endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        while (result.Count < limit)
        {
            var chunk = Math.Min(MaxBatch, limit - result.Count);
            var startTime = endTime - chunk * stepMs;

            var requestBody = new
            {
                type = "candleSnapshot",
                req = new { coin, interval = timeframe, startTime, endTime }
            };
            var response = await Http.PostAsJsonAsync("/info", requestBody);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

            var batch = new List<Bar>();
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                batch.Add(new Bar
                {
                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(row.GetProperty("t").GetInt64()).UtcDateTime,
                    Open = decimal.Parse(row.GetProperty("o").GetString()!, CultureInfo.InvariantCulture),
                    High = decimal.Parse(row.GetProperty("h").GetString()!, CultureInfo.InvariantCulture),
                    Low = decimal.Parse(row.GetProperty("l").GetString()!, CultureInfo.InvariantCulture),
                    Close = decimal.Parse(row.GetProperty("c").GetString()!, CultureInfo.InvariantCulture),
                    Volume = decimal.Parse(row.GetProperty("v").GetString()!, CultureInfo.InvariantCulture),
                });
            }

            if (batch.Count == 0) break;

            result.InsertRange(0, batch);
            endTime = startTime;
            if (batch.Count < chunk) break;
        }

        return result;
    }

    // Live mid prices (one call for all coins) + cached daily change per coin.
    public async Task<Dictionary<string, TickerInfo>> GetTickersAsync(IEnumerable<string> symbols)
    {
        var result = new Dictionary<string, TickerInfo>();

        var pairs = symbols
            .Select(s => (Original: s, Coin: s.Split('/')[0].ToUpperInvariant()))
            .Distinct()
            .ToList();
        if (pairs.Count == 0) return result;

        // 1) Current mid price for every coin in a single public call.
        var midsResponse = await Http.PostAsJsonAsync("/info", new { type = "allMids" });
        midsResponse.EnsureSuccessStatusCode();
        using var midsDoc = JsonDocument.Parse(await midsResponse.Content.ReadAsStreamAsync());

        foreach (var (original, coin) in pairs)
        {
            if (!midsDoc.RootElement.TryGetProperty(coin, out var midElement)) continue;

            result[original] = new TickerInfo
            {
                Last = decimal.Parse(midElement.GetString()!, CultureInfo.InvariantCulture),
                ChgPercent = await GetDailyChangePercentAsync(coin),
            };
        }

        return result;
    }

    // Approximates the 24h change from the last two daily candles.
    // Cached per coin for 5 minutes to keep the public API polite.
    private static async Task<double> GetDailyChangePercentAsync(string coin)
    {
        if (DailyChangeCache.TryGetValue(coin, out var cached) &&
            DateTime.UtcNow - cached.FetchedAt < TimeSpan.FromMinutes(5))
        {
            return cached.Chg;
        }

        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var body = new
            {
                type = "candleSnapshot",
                req = new { coin, interval = "1d", startTime = now - 2 * 86_400_000L, endTime = now }
            };
            var response = await Http.PostAsJsonAsync("/info", body);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

            var candles = doc.RootElement.EnumerateArray().ToList();
            double chg = 0;
            if (candles.Count >= 2)
            {
                var prevClose = double.Parse(candles[^2].GetProperty("c").GetString()!, CultureInfo.InvariantCulture);
                var lastClose = double.Parse(candles[^1].GetProperty("c").GetString()!, CultureInfo.InvariantCulture);
                if (prevClose != 0) chg = (lastClose - prevClose) / prevClose * 100;
            }
            else if (candles.Count == 1)
            {
                var open = double.Parse(candles[0].GetProperty("o").GetString()!, CultureInfo.InvariantCulture);
                var close = double.Parse(candles[0].GetProperty("c").GetString()!, CultureInfo.InvariantCulture);
                if (open != 0) chg = (close - open) / open * 100;
            }

            DailyChangeCache[coin] = (chg, DateTime.UtcNow);
            return chg;
        }
        catch
        {
            // Network hiccup: fall back to the cached value (or 0 if none).
            return cached.Chg;
        }
    }
}