using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Meowgnal.Services;

namespace Meowgnal;

public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Meowgnal", "logs", "crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        // Create log directory immediately
        var logDir = Path.GetDirectoryName(LogPath);
        if (!string.IsNullOrEmpty(logDir))
            Directory.CreateDirectory(logDir);

        // Write startup marker
        File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] App starting...\n");

        base.OnStartup(e);

        try
        {
            AppLogger.Info("App started.");
        }
        catch (Exception ex)
        {
            // AppLogger might not be initialized yet
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] AppLogger failed: {ex.Message}\n");
        }

        // Non-UI thread crashes
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] NON-UI CRASH: {ex?.Message}\n{ex?.StackTrace}\n";
            File.AppendAllText(LogPath, msg);
            Debug.WriteLine("CRASH LOGGED: " + msg);
        };

        // UI thread crashes
        DispatcherUnhandledException += (_, args) =>
        {
            var ex = args.Exception;
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UI CRASH: {ex?.Message}\n{ex?.StackTrace}\n";
            File.AppendAllText(LogPath, msg);
            Debug.WriteLine("CRASH LOGGED: " + msg);

            MessageBox.Show(
                "An unexpected error occurred and was logged.\n\n" +
                ex?.Message + "\n\nLog: " + LogPath,
                "Meowgnal — error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            args.Handled = true;
        };

        // Task-based async crashes
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            var ex = args.Exception;
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ASYNC CRASH: {ex?.Message}\n{ex?.StackTrace}\n";
            File.AppendAllText(LogPath, msg);
            Debug.WriteLine("ASYNC CRASH LOGGED: " + msg);
            args.SetObserved();
        };
    }
}