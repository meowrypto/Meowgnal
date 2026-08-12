using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Meowgnal.Models;

namespace Meowgnal.Services;

public static class JournalStorageService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Meowgnal", "journal.dat");
    private static readonly byte[] Entropy = System.Text.Encoding.UTF8.GetBytes("Meowgnal.Journal.v1");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public static JournalFile Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new JournalFile();
            var encrypted = File.ReadAllBytes(FilePath);
            var json = System.Text.Encoding.UTF8.GetString(
                ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser));
            return JsonSerializer.Deserialize<JournalFile>(json, JsonOptions) ?? new JournalFile();
        }
        catch
        {
            return new JournalFile();
        }
    }

    public static void Save(JournalFile journal)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(journal, JsonOptions);
            var encrypted = ProtectedData.Protect(
                System.Text.Encoding.UTF8.GetBytes(json),
                Entropy,
                DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FilePath, encrypted);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Journal save failed: {ex.Message}");
        }
    }

    public static void AddEntry(JournalEntry entry)
    {
        var journal = Load();
        journal.Entries.Add(entry);
        Save(journal);
    }

    public static void UpdateEntry(JournalEntry updatedEntry)
    {
        var journal = Load();
        var existing = journal.Entries.Find(e => e.EntryId == updatedEntry.EntryId);
        if (existing is not null)
        {
            existing.Notes = updatedEntry.Notes;
            existing.Tags = updatedEntry.Tags;
            existing.ScreenshotPath = updatedEntry.ScreenshotPath;
            existing.UpdatedAt = DateTime.UtcNow;
            Save(journal);
        }
    }

    public static void DeleteEntry(string entryId)
    {
        var journal = Load();
        journal.Entries.RemoveAll(e => e.EntryId == entryId);
        Save(journal);
    }
}