using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Meowgnal.Services;

namespace Meowgnal.DataProviders;

// Fetches funding rate history from Binance Futures and Hyperliquid.
// Binance: public endpoint, no API key, up to 1000 rows per call.
// Hyperliquid: public /info endpoint.
public sealed class FundingRateProvider
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

    private static readonly FundamentalCache<List<(DateTime Time, double Rate)>> Cache =
        new(TimeSpan.FromMinutes(30));

    // Binance Futures: returns funding rate history (every 8 hours) for the last ~100 days.
    public async Task<List<(DateTime Time, double Rate)>> GetBinanceHistoryAsync(string symbol, int limit = 500)
    {
        var key = $"binance_funding_{symbol}_{limit}";
        if (Cache.TryGet(key, out var cached) && cached is not null)
            return cached;

        try
        {
            var rawSymbol = symbol.Replace("/", "").ToUpperInvariant();
            var url = $"/fapi/v1/fundingRate?symbol={rawSymbol}&limit={Math.Min(limit, 1000)}";

            await using var stream = await BinanceHttp.GetStreamAsync(url);
            using var doc = await JsonDocument.ParseAsync(stream);

            var result = new List<(DateTime, double)>();
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var ts = row.GetProperty("fundingTime").GetInt64();
                var rate = double.Parse(row.GetProperty("fundingRate").GetString()!, CultureInfo.InvariantCulture);
                result.Add((DateTimeOffset.FromUnixTimeMilliseconds(ts).UtcDateTime, rate));
            }

            Cache.Set(key, result);
            return result;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"FundingRateProvider (Binance {symbol}) failed", ex);
            return new List<(DateTime, double)>();
        }
    }

    // Hyperliquid: funding history (hourly) for the last ~100 days.
    public async Task<List<(DateTime Time, double Rate)>> GetHyperliquidHistoryAsync(string symbol, int limit = 500)
    {
        var key = $"hyper_funding_{symbol}_{limit}";
        if (Cache.TryGet(key, out var cached) && cached is not null)
            return cached;

        try
        {
            var coin = symbol.Split('/')[0].ToUpperInvariant();
            var endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var startTime = endTime - (long)limit * 3_600_000L;

            var body = new
            {
                type = "fundingHistory",
                req = new { coin, startTime, endTime }
            };
            var response = await HyperHttp.PostAsJsonAsync("/info", body);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

            var result = new List<(DateTime, double)>();
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var ts = row.GetProperty("time").GetInt64();
                var rate = row.GetProperty("fundingRate").GetDouble();
                result.Add((DateTimeOffset.FromUnixTimeMilliseconds(ts).UtcDateTime, rate));
            }

            Cache.Set(key, result);
            return result;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"FundingRateProvider (Hyperliquid {symbol}) failed", ex);
            return new List<(DateTime, double)>();
        }
    }

    // Routes to the right provider based on the source name.
    public async Task<List<(DateTime Time, double Rate)>> GetHistoryAsync(string source, string symbol, int limit = 500)
    {
        return source == "hyperliquid"
            ? await GetHyperliquidHistoryAsync(symbol, limit)
            : await GetBinanceHistoryAsync(symbol, limit);
    }
}