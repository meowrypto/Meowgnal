using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Meowgnal.Services;

// Sends notifications to Telegram via the Bot API (free, no server needed).
// If the bot token is empty, methods return silently (feature is optional).
public static class TelegramNotificationService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    // Sends a plain text message. Returns true if sent successfully.
    public static async Task<bool> SendAsync(string message)
    {
        var settings = SettingsStorageService.Load();
        if (string.IsNullOrWhiteSpace(settings.TelegramBotToken) ||
            string.IsNullOrWhiteSpace(settings.TelegramChatId))
            return false;

        try
        {
            var url = $"https://api.telegram.org/bot{settings.TelegramBotToken}/sendMessage";
            var payload = new
            {
                chat_id = settings.TelegramChatId,
                text = message,
                parse_mode = "Markdown"
            };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await Http.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            AppLogger.Error("TelegramNotificationService.SendAsync failed", ex);
            return false;
        }
    }

    // Sends a formatted signal notification (entry or exit).
    public static async Task NotifySignalAsync(
        string strategyName, string signalType, string symbol, string timeframe, decimal price)
    {
        var message = $"🐱 *Meowgnal Signal*\n" +
                      $"Strategy: {strategyName}\n" +
                      $"*{signalType}*: {symbol} on {timeframe}\n" +
                      $"Price: {price:F2}";
        await SendAsync(message);
    }

    // Sends a paper-trading event notification (position open/close, SL/TP hit).
    public static async Task NotifyPaperEventAsync(string eventType, string symbol, decimal? price = null)
    {
        var message = $"🐱 *Meowgnal Paper Trade*\n" +
                      $"*{eventType}*: {symbol}";
        if (price.HasValue) message += $"\nPrice: {price.Value:F2}";
        await SendAsync(message);
    }
}