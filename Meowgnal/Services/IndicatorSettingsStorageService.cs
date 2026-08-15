using System;
using System.IO;
using System.Text.Json;
using Meowgnal.Models;

namespace Meowgnal.Services;

public static class IndicatorSettingsStorageService
{
    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Meowgnal", "indicators.json");

    public static IndicatorSettingsFile Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new IndicatorSettingsFile();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<IndicatorSettingsFile>(json) ?? new IndicatorSettingsFile();
        }
        catch
        {
            return new IndicatorSettingsFile();
        }
    }

    public static void Save(IndicatorSettingsFile file)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to save indicator settings", ex);
        }
    }
}