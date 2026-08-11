using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Meowgnal.Models;

namespace Meowgnal.Services;

/// <summary>Saves/loads price alerts to alerts.dat, encrypted with DPAPI.</summary>
public static class PriceAlertStorageService
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Meowgnal-Alerts-v1");

    public static PriceAlertsFile Load()
    {
        try
        {
            if (!File.Exists(AppPaths.AlertsFile)) return new PriceAlertsFile();

            byte[] encrypted = File.ReadAllBytes(AppPaths.AlertsFile);
            if (encrypted.Length == 0) return new PriceAlertsFile();

            byte[] plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<PriceAlertsFile>(Encoding.UTF8.GetString(plain)) ?? new PriceAlertsFile();
        }
        catch
        {
            return new PriceAlertsFile();
        }
    }

    public static void Save(PriceAlertsFile file)
    {
        Directory.CreateDirectory(AppPaths.AppDataFolder);

        string json = JsonSerializer.Serialize(file);
        byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(AppPaths.AlertsFile, encrypted);
    }
}