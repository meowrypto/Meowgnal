using System;
using System.Windows;
using Meowgnal.Services;

namespace Meowgnal;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppLogger.Info("App started.");

        // Non-UI thread crashes
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            AppLogger.Fatal("Unhandled non-UI crash", args.ExceptionObject as Exception);
        };

        // UI thread crashes: log + notify, keep the app alive
        DispatcherUnhandledException += (_, args) =>
        {
            AppLogger.Fatal("Unhandled UI crash", args.Exception);
            MessageBox.Show(
                "An unexpected error occurred. It was saved to the log file.\n\n" +
                args.Exception.Message + "\n\nLog: %AppData%\\Meowgnal\\logs\\app.log",
                "Meowgnal — error logged",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Handled = true;
        };
    }
}