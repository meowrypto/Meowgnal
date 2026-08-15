using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Meowgnal.Models;
using Meowgnal.Services;
using System.Windows.Input;



namespace Meowgnal.Views;

public partial class JournalWindow : Window
{
    private JournalFile _journal;
    private JournalEntry? _selectedEntry;
    private static readonly string ScreenshotsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Meowgnal", "Screenshots");

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { ToggleMaximize(); return; }
        if (WindowState == WindowState.Maximized)
        {
            var point = PointToScreen(e.GetPosition(this));
            WindowState = WindowState.Normal;
            Left = point.X - Width / 2;
            Top = point.Y - 15;
        }
        DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaximizeButton.Content = "⛶";
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeButton.Content = "❐";
        }
    }

    public JournalWindow()
    {
        InitializeComponent();
        LoadJournal();
    }

    private void LoadJournal()
    {
        _journal = JournalStorageService.Load();
        JournalGrid.ItemsSource = _journal.Entries;
    }

    private void JournalGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedEntry = JournalGrid.SelectedItem as JournalEntry;
        if (_selectedEntry is null)
        {
            DetailsPanel.IsEnabled = false;
            return;
        }

        DetailsPanel.IsEnabled = true;
        NotesBox.Text = _selectedEntry.Notes;
        TagsBox.Text = string.Join(", ", _selectedEntry.Tags);

        if (!string.IsNullOrEmpty(_selectedEntry.ScreenshotPath) && File.Exists(_selectedEntry.ScreenshotPath))
        {
            try
            {
                ScreenshotImage.Source = new BitmapImage(new Uri(_selectedEntry.ScreenshotPath));
            }
            catch
            {
                ScreenshotImage.Source = null;
            }
        }
        else
        {
            ScreenshotImage.Source = null;
        }
    }

    private void AttachScreenshot_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry is null) return;

        var dialog = new OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            Title = "Select Screenshot"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                Directory.CreateDirectory(ScreenshotsDir);
                var ext = Path.GetExtension(dialog.FileName);
                var destPath = Path.Combine(ScreenshotsDir, $"{_selectedEntry.EntryId}_{DateTime.Now:yyyyMMddHHmmss}{ext}");

                File.Copy(dialog.FileName, destPath, true);
                _selectedEntry.ScreenshotPath = destPath;

                ScreenshotImage.Source = new BitmapImage(new Uri(destPath));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to attach screenshot: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry is null) return;

        _selectedEntry.Notes = NotesBox.Text;
        _selectedEntry.Tags = [.. TagsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        _selectedEntry.UpdatedAt = DateTime.UtcNow;

        JournalStorageService.UpdateEntry(_selectedEntry);
        JournalGrid.Items.Refresh();

        MessageBox.Show("Trade updated successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry is null) return;

        var result = MessageBox.Show(
            "Are you sure you want to delete this journal entry?\nThis action cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            JournalStorageService.DeleteEntry(_selectedEntry.EntryId);
            LoadJournal();
            DetailsPanel.IsEnabled = false;
        }
    }
}