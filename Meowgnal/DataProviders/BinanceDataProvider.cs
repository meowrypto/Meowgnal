using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Meowgnal.Models;

namespace Meowgnal.DataProviders;

// Uses Binance's public market-data API — no API key needed, since this
// is public price data, not a personal account.
// Binance caps one klines request at 1000 rows, so deeper history is
// fetched page-by-page going backwards in time.
public sealed class BinanceDataProvider : IDataProvider
{
    private const int MaxBatch = 1000;

    public string Name => "binance";

    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://api.binance.com")
    };

    public async Task<List<Bar>> GetHistoricalCandlesAsync(string symbol, string timeframe, int limit = 200)
    {
        // Our strategy format uses "BTC/USDT"; Binance expects "BTCUSDT".
        var binanceSymbol = symbol.Replace("/", "").ToUpperInvariant();

        var result = new List<Bar>(limit);
        long? endTime = null;

        while (result.Count < limit)
        {
            var batchSize = Math.Min(MaxBatch, limit - result.Count);
            var url = $"/api/v3/klines?symbol={binanceSymbol}&interval={timeframe}&limit={batchSize}";
            if (endTime.HasValue) url += $"&endTime={endTime.Value}";

            await using var stream = await Http.GetStreamAsync(url);
            using var doc = await JsonDocument.ParseAsync(stream);

            var batch = new List<Bar>();
            var firstOpenMs = 0L;
            var first = true;

            foreach (var row in doc.RootElement.EnumerateArray())
            {
                // Binance kline row: [openTime, open, high, low, close, volume, closeTime, ...]
                var openTimeMs = row[0].GetInt64();
                if (first) { firstOpenMs = openTimeMs; first = false; }

                batch.Add(new Bar
                {
                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(openTimeMs).UtcDateTime,
                    Open = decimal.Parse(row[1].GetString()!, CultureInfo.InvariantCulture),
                    High = decimal.Parse(row[2].GetString()!, CultureInfo.InvariantCulture),
                    Low = decimal.Parse(row[3].GetString()!, CultureInfo.InvariantCulture),
                    Close = decimal.Parse(row[4].GetString()!, CultureInfo.InvariantCulture),
                    Volume = decimal.Parse(row[5].GetString()!, CultureInfo.InvariantCulture),
                });
            }

            if (batch.Count == 0) break;

            // Each page is older than what we already collected → prepend it.
            result.InsertRange(0, batch);

            // Next (older) page ends just before this page's oldest candle.
            endTime = firstOpenMs - 1;
            if (batch.Count < batchSize) break;
        }

        return result;
    }

    // Live last price + 24h change for many symbols in ONE public call.
    public async Task<Dictionary<string, TickerInfo>> GetTickersAsync(IEnumerable<string> symbols)
    {
        var result = new Dictionary<string, TickerInfo>();

        var pairs = symbols
            .Select(s => (Original: s, Raw: s.Replace("/", "").ToUpperInvariant()))
            .Distinct()
            .ToList();
        if (pairs.Count == 0) return result;

        // The 24hr ticker endpoint accepts a JSON array of raw symbols.
        var symbolsJson = Uri.EscapeDataString(JsonSerializer.Serialize(pairs.Select(p => p.Raw).ToList()));
        var url = $"/api/v3/ticker/24hr?symbols={symbolsJson}";

        await using var stream = await Http.GetStreamAsync(url);
        using var doc = await JsonDocument.ParseAsync(stream);

        var rawToOriginal = pairs.ToDictionary(p => p.Raw, p => p.Original);

        foreach (var row in doc.RootElement.EnumerateArray())
        {
            var raw = row.GetProperty("symbol").GetString()!;
            if (!rawToOriginal.TryGetValue(raw, out var original)) continue;

            result[original] = new TickerInfo
            {
                Last = decimal.Parse(row.GetProperty("lastPrice").GetString()!, CultureInfo.InvariantCulture),
                ChgPercent = double.Parse(row.GetProperty("priceChangePercent").GetString()!, CultureInfo.InvariantCulture),
            };
        }

        return result;
    }
}