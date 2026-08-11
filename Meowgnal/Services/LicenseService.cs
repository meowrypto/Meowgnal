using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Meowgnal.Models;

namespace Meowgnal.Services;

/// <summary>Hardware-bound demo trial + license helpers (no cloud in v1).</summary>
public static class LicenseService
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetVolumeInformation(
        string rootPathName, StringBuilder volumeNameBuffer, int volumeNameSize,
        out uint volumeSerialNumber, out uint maximumComponentLength, out uint fileSystemFlags,
        StringBuilder fileSystemNameBuffer, int fileSystemNameSize);

    /// <summary>Stable per-PC id: machine + user + C: volume serial (hashed).</summary>
    public static string GetHardwareId()
    {
        uint serial = 0;
        try
        {
            GetVolumeInformation(@"C:\", new StringBuilder(256), 256, out serial,
                out _, out _, new StringBuilder(256), 256);
        }
        catch
        {
        }

        var raw = $"{Environment.MachineName}|{Environment.UserName}|{serial:X8}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..16];
    }

    public static void EnsureDemoStarted(AppSettings s)
    {
        if (s.DemoStartDate == DateTime.MinValue)
            s.DemoStartDate = DateTime.UtcNow;
    }

    public static int RemainingDays(AppSettings s)
    {
        EnsureDemoStarted(s);
        var used = (DateTime.UtcNow - s.DemoStartDate).TotalDays;
        return Math.Max(0, s.DemoTrialDays - (int)Math.Floor(used));
    }

    /// <summary>Licensed users are never blocked; guests get the hardware-locked trial.</summary>
    public static (bool Allowed, string Message) CheckAccess(AppSettings s)
    {
        if (!string.IsNullOrWhiteSpace(s.LicenseKey))
            return (true, "Licensed");

        var left = RemainingDays(s);
        if (left > 0)
            return (true, $"Guest demo — {left} day(s) left on this PC");

        return (false, "Guest demo expired. Activate a license from the profile menu.");
    }
}