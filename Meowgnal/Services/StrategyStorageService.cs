using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Meowgnal.Models;

namespace Meowgnal.Services;

// Saves/loads strategies as plain, human-readable JSON files (.mgstrat).
// Strategies contain no secrets (no API keys), so encryption is unnecessary.
// Plain JSON makes strategies exportable, git-friendly and shareable on GitHub.
public static class StrategyStorageService
{
    private const string FileExtension = ".mgstrat";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    private static bool _migrated;

    public static string GetFilePath(string strategyId)
        => Path.Combine(AppPaths.StrategiesFolder, strategyId + FileExtension);

    // Converts old DPAPI-encrypted files to plain JSON once per session.
    // Files that are already plain JSON (or unreadable) are skipped silently.
    public static void Migrate()
    {
        try
        {
            if (!Directory.Exists(AppPaths.StrategiesFolder)) return;

            foreach (var file in Directory.GetFiles(AppPaths.StrategiesFolder, "*" + FileExtension))
            {
                try
                {
                    var bytes = File.ReadAllBytes(file);
                    if (IsPlainJson(bytes)) continue;

                    var plainBytes = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
                    var json = Encoding.UTF8.GetString(plainBytes);
                    File.WriteAllText(file, json, Utf8NoBom);
                }
                catch
                {
                    // Not an old encrypted file (or unreadable) — skip without error.
                }
            }
        }
        catch
        {
            // Migration must never break app startup.
        }
    }

    public static void Save(StrategyDefinition strategy)
    {
        Directory.CreateDirectory(AppPaths.StrategiesFolder);
        var json = JsonSerializer.Serialize(strategy, WriteOptions);
        File.WriteAllText(GetFilePath(strategy.StrategyId), json, Utf8NoBom);
    }

    public static void Delete(string strategyId)
    {
        var path = GetFilePath(strategyId);
        if (File.Exists(path)) File.Delete(path);
    }

    // Writes a copy of the strategy JSON to any user-chosen path.
    public static void Export(StrategyDefinition strategy, string filePath)
    {
        var json = JsonSerializer.Serialize(strategy, WriteOptions);
        File.WriteAllText(filePath, json, Utf8NoBom);
    }

    // Reads a strategy from any file path (.mgstrat or .json).
    // If a strategy with the same id already exists, a new id is assigned
    // so the existing strategy is never overwritten.
    public static StrategyDefinition? Import(string filePath)
    {
        try
        {
            var strategy = DeserializeAnyFormat(File.ReadAllBytes(filePath));
            if (strategy is null) return null;

            if (File.Exists(GetFilePath(strategy.StrategyId)))
                strategy.StrategyId = Guid.NewGuid().ToString("N");

            Save(strategy);
            return strategy;
        }
        catch
        {
            return null;
        }
    }

    public static StrategyDefinition? Load(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        try
        {
            return DeserializeAnyFormat(File.ReadAllBytes(filePath));
        }
        catch
        {
            return null;
        }
    }

    public static List<StrategyDefinition> LoadAll()
    {
        if (!_migrated)
        {
            _migrated = true;
            Migrate();
        }

        var result = new List<StrategyDefinition>();
        if (!Directory.Exists(AppPaths.StrategiesFolder)) return result;

        foreach (var file in Directory.GetFiles(AppPaths.StrategiesFolder, "*" + FileExtension))
        {
            var strategy = Load(file);
            if (strategy is not null) result.Add(strategy);
        }
        return result;
    }

    // Accepts both the new plain-JSON format and the old DPAPI-encrypted format.
    private static StrategyDefinition? DeserializeAnyFormat(byte[] bytes)
    {
        if (IsPlainJson(bytes))
        {
            return JsonSerializer.Deserialize<StrategyDefinition>(Encoding.UTF8.GetString(bytes));
        }

        var plainBytes = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize<StrategyDefinition>(Encoding.UTF8.GetString(plainBytes));
    }

    // Plain JSON files always start with '{' (optionally after a UTF-8 BOM).
    private static bool IsPlainJson(byte[] bytes)
    {
        var i = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) i = 3;
        return bytes.Length > i && bytes[i] == (byte)'{';
    }
}