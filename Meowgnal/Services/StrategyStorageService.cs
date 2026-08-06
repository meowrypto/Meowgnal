using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Meowgnal.Models;

namespace Meowgnal.Services;

// Saves/loads strategies as DPAPI-encrypted files.
// The encryption key never leaves Windows and is tied to the current
// Windows user account + machine — no export, no manual key management.
public static class StrategyStorageService
{
    private const string FileExtension = ".mgstrat";

    public static string GetFilePath(string strategyId)
        => Path.Combine(AppPaths.StrategiesFolder, strategyId + FileExtension);

    public static void Save(StrategyDefinition strategy)
    {
        Directory.CreateDirectory(AppPaths.StrategiesFolder);

        var json = JsonSerializer.Serialize(strategy);
        var plainBytes = Encoding.UTF8.GetBytes(json);

        // DataProtectionScope.CurrentUser: only decryptable by this Windows
        // user on this machine — this is our "only the app can read it" rule.
        var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

        File.WriteAllBytes(GetFilePath(strategy.StrategyId), encryptedBytes);
    }

    public static StrategyDefinition? Load(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        byte[] plainBytes;
        try
        {
            var encryptedBytes = File.ReadAllBytes(filePath);
            plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            // Copied from another machine/user, or the file was tampered with.
            return null;
        }

        var json = Encoding.UTF8.GetString(plainBytes);
        return JsonSerializer.Deserialize<StrategyDefinition>(json);
    }

    public static List<StrategyDefinition> LoadAll()
    {
        var result = new List<StrategyDefinition>();
        if (!Directory.Exists(AppPaths.StrategiesFolder)) return result;

        foreach (var file in Directory.GetFiles(AppPaths.StrategiesFolder, "*" + FileExtension))
        {
            var strategy = Load(file);
            if (strategy is not null) result.Add(strategy);
        }
        return result;
    }
}