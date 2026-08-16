using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Meowgnal.Models;

namespace Meowgnal.Views;

// One row in the prompt UI, with live "Yes" detection for critical items.
public sealed class ChecklistItemVm : INotifyPropertyChanged
{
    public string Id { get; set; } = "";
    public string Question { get; set; } = "";
    public bool IsCritical { get; set; }

    private bool _answer;
    public bool Answer
    {
        get => _answer;
        set { _answer = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Answer))); }
    }

    public Visibility CriticalVisibility => IsCritical ? Visibility.Visible : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;
}

// The result chosen by the user: confirmed (with answers) or skipped.
public sealed class ChecklistPromptResult
{
    public ChecklistResult? Result { get; set; }
    public bool Skipped { get; set; }
}

public partial class ChecklistPromptWindow : Window
{
    private readonly ObservableCollection<ChecklistItemVm> _items = new();
    public ChecklistPromptResult Result { get; private set; } = new();

    public ChecklistPromptWindow(System.Collections.Generic.List<ChecklistItem> items)
    {
        InitializeComponent();
        foreach (var item in items)
            _items.Add(new ChecklistItemVm
            {
                Id = item.Id,
                Question = item.Question,
                IsCritical = item.IsCritical,
                Answer = false
            });
        QuestionsList.ItemsSource = _items;

        // Re-evaluate the soft warning whenever any checkbox changes.
        foreach (var item in _items)
            item.PropertyChanged += (_, _) => UpdateSoftWarning();
    }

    #region Custom title bar

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            var point = PointToScreen(e.GetPosition(this));
            WindowState = WindowState.Normal;
            Left = point.X - Width / 2;
            Top = point.Y - 15;
        }
        DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Result = new ChecklistPromptResult { Skipped = true };
        DialogResult = false;
        Close();
    }

    #endregion

    private void UpdateSoftWarning()
    {
        var triggered = _items.Where(i => i.IsCritical && i.Answer).ToList();
        if (triggered.Count == 0)
        {
            SoftWarningBanner.Visibility = Visibility.Collapsed;
            return;
        }
        SoftWarningBanner.Visibility = Visibility.Visible;
        var phrases = triggered.Select(i => SoftHint(i.Id)).ToList();
        SoftWarningText.Text = "😿 " + string.Join(" ", phrases);
    }

    private static string SoftHint(string id) => id switch
    {
        "revenge" => "Maybe give it 10 minutes before this one?",
        "emotion" => "A short walk might help more than a trade right now.",
        "risk_limit" => "This is bigger than your normal risk — are you sure?",
        _ => "Take a breath before confirming."
    };

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var result = new ChecklistResult { Timestamp = DateTime.UtcNow, Skipped = false };
        foreach (var item in _items)
            result.Answers.Add(new ChecklistAnswer
            {
                QuestionId = item.Id,
                QuestionText = item.Question,
                IsCritical = item.IsCritical,
                Answer = item.Answer
            });
        Result = new ChecklistPromptResult { Result = result, Skipped = false };
        DialogResult = true;
        Close();
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        Result = new ChecklistPromptResult
        {
            Result = new ChecklistResult { Timestamp = DateTime.UtcNow, Skipped = true },
            Skipped = true
        };
        DialogResult = true;
        Close();
    }
}