using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Meowgnal.Models;

namespace Meowgnal.Services;

/// <summary>
/// Encrypted (DPAPI) storage for the paper trading account.
/// Persists balance, open positions and trade history across app restarts.
/// </summary>
public static class PaperAccountStorageService
{
    private static readonly string FilePath = AppPaths.PaperAccountFile;
    private static readonly byte[] Entropy = System.Text.Encoding.UTF8.GetBytes("Meowgnal.Paper.v1");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public static PaperAccountFile Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new PaperAccountFile();
            var encrypted = File.ReadAllBytes(FilePath);
            var json = System.Text.Encoding.UTF8.GetString(ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser));
            return JsonSerializer.Deserialize<PaperAccountFile>(json, JsonOptions) ?? new PaperAccountFile();
        }
        catch
        {
            return new PaperAccountFile();
        }
    }

    public static void Save(PaperAccountFile account)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(account, JsonOptions);
            var encrypted = ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(json), Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FilePath, encrypted);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PaperAccount save failed: {ex.Message}");
        }
    }
}