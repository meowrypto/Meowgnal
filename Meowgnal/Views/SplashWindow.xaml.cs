using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace Meowgnal.Views;

public partial class SplashWindow : Window
{
    // The splash stays visible for at least this many milliseconds.
    private const double MinShowMilliseconds = 4000;

    private readonly DateTime _shownAt = DateTime.UtcNow;
    private bool _closeScheduled;

    public SplashWindow()
    {
        InitializeComponent();

        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        VersionText.Text = version is null
            ? "Version 1.0"
            : $"Version {version.Major}.{version.Minor}.{version.Build}";

        try
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exe))
            {
                var date = File.GetLastWriteTime(exe);
                UpdateText.Text = $"Last update: {date:yyyy/MM/dd}";
            }
        }
        catch
        {
            UpdateText.Text = "Last update: —";
        }
    }

    // Keeps the splash on screen for at least MinShowMilliseconds,
    // no matter when the app tries to close it.
    protected override void OnClosing(CancelEventArgs e)
    {
        var elapsedMs = (DateTime.UtcNow - _shownAt).TotalMilliseconds;

        // Enough time passed: close normally.
        if (elapsedMs >= MinShowMilliseconds)
            return;

        // Block this early close request.
        e.Cancel = true;

        // Schedule the real close only once.
        if (_closeScheduled)
            return;

        _closeScheduled = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(MinShowMilliseconds - elapsedMs) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Close();
        };
        timer.Start();
    }
}