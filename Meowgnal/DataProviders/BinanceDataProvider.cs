using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Meowgnal.Models;

namespace Meowgnal.DataProviders;

// Uses Binance's public market-data API — no API key needed, since this
// is public price data, not a personal account.
public sealed class BinanceDataProvider : IDataProvider
{
    public string Name => "binance";

    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://api.binance.com")
    };

    public async Task<List<Bar>> GetHistoricalCandlesAsync(string symbol, string timeframe, int limit = 200)
    {
        // Our strategy format uses "BTC/USDT"; Binance expects "BTCUSDT".
        var binanceSymbol = symbol.Replace("/", "").ToUpperInvariant();

        var url = $"/api/v3/klines?symbol={binanceSymbol}&interval={timeframe}&limit={limit}";
        await using var stream = await Http.GetStreamAsync(url);
        using var doc = await JsonDocument.ParseAsync(stream);

        var bars = new List<Bar>();
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            // Binance kline row: [openTime, open, high, low, close, volume, closeTime, ...]
            var openTimeMs = row[0].GetInt64();
            bars.Add(new Bar
            {
                OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(openTimeMs),
                Open = decimal.Parse(row[1].GetString()!, CultureInfo.InvariantCulture),
                High = decimal.Parse(row[2].GetString()!, CultureInfo.InvariantCulture),
                Low = decimal.Parse(row[3].GetString()!, CultureInfo.InvariantCulture),
                Close = decimal.Parse(row[4].GetString()!, CultureInfo.InvariantCulture),
                Volume = decimal.Parse(row[5].GetString()!, CultureInfo.InvariantCulture),
            });
        }
        return bars;
    }
}