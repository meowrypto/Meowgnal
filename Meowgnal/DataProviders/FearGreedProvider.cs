using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Meowgnal.Services;

namespace Meowgnal.DataProviders;

// Fetches the Fear & Greed Index from alternative.me (free, no API key).
// Caches the full 100-day history for 1 hour.
public sealed class FearGreedProvider
{
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://api.alternative.me"),
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static readonly FundamentalCache<List<(DateTime Date, double Value)>> Cache =
        new(TimeSpan.FromHours(1));

    // Returns up to 100 daily readings (newest first).
    // Empty list if the API is unreachable.
    public async Task<List<(DateTime Date, double Value)>> GetHistoryAsync()
    {
        if (Cache.TryGet("fng", out var cached) && cached is not null)
            return cached;

        try
        {
            await using var stream = await Http.GetStreamAsync("/fng/?limit=100");
            using var doc = await JsonDocument.ParseAsync(stream);

            if (!doc.RootElement.TryGetProperty("data", out var data))
                return new List<(DateTime, double)>();

            var result = new List<(DateTime, double)>();
            foreach (var row in data.EnumerateArray())
            {
                var ts = row.GetProperty("timestamp").GetInt64();
                var value = double.Parse(row.GetProperty("value").GetString()!, CultureInfo.InvariantCulture);
                var date = DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime.Date;
                result.Add((date, value));
            }

            Cache.Set("fng", result);
            return result;
        }
        catch (Exception ex)
        {
            AppLogger.Error("FearGreedProvider failed", ex);
            return new List<(DateTime, double)>();
        }
    }

    // Returns the latest value, or null if unavailable.
    public async Task<double?> GetLatestAsync()
    {
        var history = await GetHistoryAsync();
        return history.Count > 0 ? history[0].Value : null;
    }
}