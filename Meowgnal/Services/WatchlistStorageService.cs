using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Meowgnal.Models;

namespace Meowgnal.Services;

// Saves/loads all watchlists as one DPAPI-encrypted file — same security
// approach as strategies and settings (tied to this Windows user + machine).
public static class WatchlistStorageService
{
    public static WatchlistsFile Load()
    {
        if (!File.Exists(AppPaths.WatchlistsFile))
        {
            // First run: one default list with popular pairs.
            return new WatchlistsFile
            {
                ActiveListName = "Main",
                Lists = new()
                {
                    new WatchlistDefinition
                    {
                        Name = "Main",
                        Items = new()
                        {
                            new WatchlistItem { Symbol = "BTC/USDT", DataSource = "binance" },
                            new WatchlistItem { Symbol = "ETH/USDT", DataSource = "binance" },
                            new WatchlistItem { Symbol = "SOL/USDT", DataSource = "binance" },
                            new WatchlistItem { Symbol = "BNB/USDT", DataSource = "binance" },
                            new WatchlistItem { Symbol = "XRP/USDT", DataSource = "binance" },
                            new WatchlistItem { Symbol = "DOGE/USDT", DataSource = "binance" },
                        }
                    }
                }
            };
        }

        try
        {
            var encryptedBytes = File.ReadAllBytes(AppPaths.WatchlistsFile);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plainBytes);
            return JsonSerializer.Deserialize<WatchlistsFile>(json) ?? new WatchlistsFile();
        }
        catch (CryptographicException)
        {
            // Copied from another machine/user, or tampered with.
            return new WatchlistsFile();
        }
    }

    public static void Save(WatchlistsFile file)
    {
        Directory.CreateDirectory(AppPaths.AppDataFolder);
        var json = JsonSerializer.Serialize(file);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(AppPaths.WatchlistsFile, encryptedBytes);
    }
}