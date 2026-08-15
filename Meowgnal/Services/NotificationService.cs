using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Meowgnal.Services;

// Central place for user-facing alerts: Windows toast + alert sound + Telegram.
// All are optional and controlled by Settings -> Notifications.
public static class NotificationService
{
    private static string? _alertSoundPath;

    // Shows a real Windows toast notification in the corner of the screen.
    public static void ShowToast(string title, string message)
    {
        try
        {
            var xml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            var textNodes = xml.GetElementsByTagName("text");
            textNodes[0].AppendChild(xml.CreateTextNode(title));
            textNodes[1].AppendChild(xml.CreateTextNode(message));
            ToastNotificationManager.CreateToastNotifier("Meowgnal").Show(new ToastNotification(xml));
        }
        catch
        {
            // A notification must never crash the app.
        }
    }

    // Plays a short generated beep (no external audio file needed).
    public static void PlayAlertSound()
    {
        try
        {
            EnsureAlertSoundFile();
            if (_alertSoundPath is null) return;
            var player = new MediaPlayer();
            player.Open(new Uri(_alertSoundPath));
            player.Play();
        }
        catch
        {
            // A sound must never crash the app.
        }
    }

    // Sends both local toast and Telegram notification for a signal.
    public static async void NotifySignal(
        string strategyName, string signalType, string symbol, string timeframe, decimal price)
    {
        var settings = SettingsStorageService.Load();
        if (settings.ToastNotificationsEnabled)
            ShowToast("Meowgnal Signal", $"{signalType}: {symbol} on {timeframe} @ {price:F2}");

        await TelegramNotificationService.NotifySignalAsync(strategyName, signalType, symbol, timeframe, price);
    }

    // Sends both local toast and Telegram notification for a paper-trading event.
    public static async void NotifyPaperEvent(string eventType, string symbol, decimal? price = null)
    {
        var settings = SettingsStorageService.Load();
        if (settings.ToastNotificationsEnabled)
        {
            var msg = price.HasValue ? $"{eventType}: {symbol} @ {price.Value:F2}" : $"{eventType}: {symbol}";
            ShowToast("Meowgnal Paper Trade", msg);
        }

        await TelegramNotificationService.NotifyPaperEventAsync(eventType, symbol, price);
    }

    private static void EnsureAlertSoundFile()
    {
        if (_alertSoundPath is not null && File.Exists(_alertSoundPath)) return;

        var path = Path.Combine(AppPaths.AppDataFolder, "alert.wav");
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(AppPaths.AppDataFolder);
            File.WriteAllBytes(path, BuildBeepWav());
        }
        _alertSoundPath = path;
    }

    // Builds a 0.5 second 880Hz sine wave with fade-out (22050Hz mono 16-bit PCM).
    private static byte[] BuildBeepWav()
    {
        const int sampleRate = 22050;
        const double seconds = 0.5;
        const double frequency = 880.0;
        var sampleCount = (int)(sampleRate * seconds);
        var samples = new short[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (double)sampleRate;
            var fade = 1.0 - i / (double)sampleCount;
            var value = Math.Sin(2 * Math.PI * frequency * t) * fade * 0.6;
            samples[i] = (short)(value * short.MaxValue);
        }

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // RIFF / WAV header
        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + sampleCount * 2);
        writer.Write(new[] { 'W', 'A', 'V', 'E' });

        // fmt chunk
        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((short)1);          // PCM
        writer.Write((short)1);          // mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);    // bytes per second
        writer.Write((short)2);          // block align
        writer.Write((short)16);         // bits per sample

        // data chunk
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(sampleCount * 2);
        foreach (var s in samples) writer.Write(s);

        return ms.ToArray();
    }
}