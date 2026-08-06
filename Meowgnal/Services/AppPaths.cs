using System;
using System.IO;

namespace Meowgnal.Services;

// Central place for all folders the app writes to on disk.
public static class AppPaths
{
    public static string AppDataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Meowgnal");

    public static string StrategiesFolder =>
        Path.Combine(AppDataFolder, "Strategies");
}