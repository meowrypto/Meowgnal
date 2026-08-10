using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Meowgnal.Models;

namespace Meowgnal.Services;

/// <summary>Saves/loads drawings to drawings.dat, encrypted with DPAPI.</summary>
public static class DrawingStorageService
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Meowgnal-Drawings-v1");

    public static DrawingsFile Load()
    {
        try
        {
            if (!File.Exists(AppPaths.DrawingsFile)) return new DrawingsFile();

            byte[] encrypted = File.ReadAllBytes(AppPaths.DrawingsFile);
            if (encrypted.Length == 0) return new DrawingsFile();

            byte[] plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            string json = Encoding.UTF8.GetString(plain);

            return JsonSerializer.Deserialize<DrawingsFile>(json) ?? new DrawingsFile();
        }
        catch
        {
            // File corrupted or inaccessible: continue with an empty list.
            return new DrawingsFile();
        }
    }

    public static void Save(DrawingsFile file)
    {
        Directory.CreateDirectory(AppPaths.AppDataFolder);

        string json = JsonSerializer.Serialize(file);
        byte[] plain = Encoding.UTF8.GetBytes(json);
        byte[] encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);

        File.WriteAllBytes(AppPaths.DrawingsFile, encrypted);
    }
}