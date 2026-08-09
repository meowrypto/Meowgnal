using System;
using System.Collections.Generic;
using System.Globalization;
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
}