using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Meowgnal.Services;

namespace Meowgnal.DataProviders;

// Fetches BTC market dominance from CoinGecko (free, no API key).
// CoinGecko's free /global endpoint returns only current snapshot,
// so this provider has no historical series for backtesting.
// Caches the current value for 5 minutes.
public sealed class MarketDominanceProvider
{
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://api.coingecko.com"),
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static readonly FundamentalCache<double> Cache = new(TimeSpan.FromMinutes(5));

    // Returns BTC dominance as a percent (e.g. 54.3 means 54.3%).
    // Returns null if the API is unreachable.
    public async Task<double?> GetBtcDominanceAsync()
    {
        if (Cache.TryGet("btc_dom", out var cached))
            return cached;

        try
        {
            await using var stream = await Http.GetStreamAsync("/api/v3/global");
            using var doc = await JsonDocument.ParseAsync(stream);

            if (!doc.RootElement.TryGetProperty("data", out var data))
                return null;
            if (!data.TryGetProperty("market_cap_percentage", out var pct))
                return null;
            if (!pct.TryGetProperty("btc", out var btcElement))
                return null;

            var value = btcElement.GetDouble();
            Cache.Set("btc_dom", value);
            return value;
        }
        catch (Exception ex)
        {
            AppLogger.Error("MarketDominanceProvider failed", ex);
            return null;
        }
    }
}