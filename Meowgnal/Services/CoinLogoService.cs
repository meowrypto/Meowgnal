using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Meowgnal.Services;

// Downloads and caches the official coin logos (CoinMarketCap CDN icons) —
// the same artwork shown on exchanges and CMC. Cached on disk so it works
// offline after the first run. Falls back to a text badge for unknown coins.
public static class CoinLogoService
{
    private static readonly object Lock = new();
    private static readonly Dictionary<string, BitmapImage> MemoryCache = new();
    private static readonly HashSet<string> Failed = new();

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    // CoinMarketCap ids for the most common coins.
    private static readonly Dictionary<string, int> CmcIds = new()
    {
        ["BTC"] = 1,
        ["ETH"] = 1027,
        ["BNB"] = 1839,
        ["SOL"] = 5426,
        ["XRP"] = 52,
        ["DOGE"] = 74,
        ["ADA"] = 2010,
        ["AVAX"] = 5805,
        ["DOT"] = 6636,
        ["LINK"] = 1975,
        ["MATIC"] = 3890,
        ["POL"] = 28321,
        ["LTC"] = 2,
        ["TRX"] = 1958,
        ["UNI"] = 7083,
        ["ATOM"] = 3794,
        ["XLM"] = 512,
        ["NEAR"] = 6535,
        ["APT"] = 21794,
        ["ARB"] = 11841,
        ["OP"] = 11840,
        ["INJ"] = 7226,
        ["SUI"] = 20947,
        ["PEPE"] = 24478,
        ["SHIB"] = 5994,
        ["FIL"] = 2280,
        ["ETC"] = 1321,
        ["BCH"] = 1831,
        ["TON"] = 11419,
        ["WIF"] = 29303,
    };

    private static string CacheFolder =>
        Path.Combine(AppPaths.AppDataFolder, "logos");

    public static bool TryGetCached(string coin, out BitmapImage? image)
    {
        lock (Lock)
        {
            if (MemoryCache.TryGetValue(coin, out var img)) { image = img; return true; }
            image = null;
            return false;
        }
    }

    public static async Task<BitmapImage?> LoadAsync(string coin)
    {
        lock (Lock)
        {
            if (MemoryCache.TryGetValue(coin, out var cached)) return cached;
            if (Failed.Contains(coin)) return null;
        }

        // 1) Disk cache
        var diskPath = Path.Combine(CacheFolder, coin + ".png");
        try
        {
            if (File.Exists(diskPath))
            {
                var fromDisk = FromFile(diskPath);
                if (fromDisk is not null) { Remember(coin, fromDisk); return fromDisk; }
            }
        }
        catch { }

        // 2) Network (CoinMarketCap CDN)
        if (!CmcIds.TryGetValue(coin, out var id)) { RememberFailure(coin); return null; }

        try
        {
            var bytes = await Http.GetByteArrayAsync($"https://s2.coinmarketcap.com/static/img/coins/64x64/{id}.png");
            using var ms = new MemoryStream(bytes);
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            bi.Freeze();

            try
            {
                Directory.CreateDirectory(CacheFolder);
                File.WriteAllBytes(diskPath, bytes);
            }
            catch { }

            Remember(coin, bi);
            return bi;
        }
        catch
        {
            RememberFailure(coin);
            return null;
        }
    }

    private static BitmapImage? FromFile(string path)
    {
        using var ms = new MemoryStream(File.ReadAllBytes(path));
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.StreamSource = ms;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    private static void Remember(string coin, BitmapImage img)
    {
        lock (Lock) { MemoryCache[coin] = img; }
    }

    private static void RememberFailure(string coin)
    {
        lock (Lock) { Failed.Add(coin); }
    }
}