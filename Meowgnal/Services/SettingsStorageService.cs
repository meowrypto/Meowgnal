using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Meowgnal.Models;

namespace Meowgnal.Services;

// Same DPAPI approach as StrategyStorageService — encrypted, tied to this
// Windows user + machine. Used here mainly to protect the API key fields.
public static class SettingsStorageService
{
    private static string FilePath => Path.Combine(AppPaths.AppDataFolder, "settings.dat");

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(AppPaths.AppDataFolder);
        var json = JsonSerializer.Serialize(settings);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FilePath, encryptedBytes);
    }

    public static AppSettings Load()
    {
        if (!File.Exists(FilePath)) return new AppSettings();

        try
        {
            var encryptedBytes = File.ReadAllBytes(FilePath);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plainBytes);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (CryptographicException)
        {
            return new AppSettings();
        }
    }
}