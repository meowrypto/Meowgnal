using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;

namespace Meowgnal.Views;

public partial class SplashWindow : Window
{
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
}