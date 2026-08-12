using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Meowgnal.Models;

namespace Meowgnal.Services;

/// <summary>A reusable bundle of drawings (cross-symbol template).</summary>
public sealed class DrawingTemplate
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;
    public string SourceSymbol { get; set; } = "";
    public List<Drawing> Drawings { get; set; } = new();
}

/// <summary>Import/export drawing templates as plain JSON files.</summary>
public static class TemplateService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static bool Export(DrawingTemplate template, string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(template, Options);
            File.WriteAllText(path, json);
            AppLogger.Info($"Template exported: {template.Name} ({template.Drawings.Count} drawings)");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Template export failed", ex);
            return false;
        }
    }

    public static DrawingTemplate? Import(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            var t = JsonSerializer.Deserialize<DrawingTemplate>(json);
            AppLogger.Info($"Template imported: {t?.Name} ({t?.Drawings.Count ?? 0} drawings)");
            return t;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Template import failed", ex);
            return null;
        }
    }
}