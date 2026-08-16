using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Meowgnal.Models;

namespace Meowgnal.Views;

// Small editor for checklist questions: add / remove / edit / mark critical.
public partial class ChecklistEditorWindow : Window
{
    private readonly List<ChecklistItem> _items;
    private readonly List<(TextBox Box, CheckBox Critical, ChecklistItem Item)> _rows = new();

    public List<ChecklistItem> EditedList { get; private set; } = new();

    public ChecklistEditorWindow(List<ChecklistItem> items)
    {
        InitializeComponent();
        _items = items.Select(i => new ChecklistItem { Id = i.Id, Question = i.Question, IsCritical = i.IsCritical }).ToList();
        RebuildRows();
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

    #endregion

    private void RebuildRows()
    {
        RowsPanel.Children.Clear();
        _rows.Clear();

        foreach (var item in _items)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var box = new TextBox { Text = item.Question, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(box, 0);

            var crit = new CheckBox { Content = "Critical", IsChecked = item.IsCritical, Margin = new Thickness(8, 0, 0, 0) };
            Grid.SetColumn(crit, 1);

            var del = new Button
            {
                Content = "🗑",
                Style = (Style)FindResource("SecondaryButton"),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(6, 0, 0, 0),
                Tag = item
            };
            del.Click += DeleteRow_Click;
            Grid.SetColumn(del, 2);

            row.Children.Add(box);
            row.Children.Add(crit);
            row.Children.Add(del);
            RowsPanel.Children.Add(row);
            _rows.Add((box, crit, item));
        }
    }

    private void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not ChecklistItem item) return;
        _items.Remove(item);
        RebuildRows();
    }

    private void AddRow_Click(object sender, RoutedEventArgs e)
    {
        _items.Add(new ChecklistItem { Question = "" });
        RebuildRows();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var list = new List<ChecklistItem>();
        foreach (var (box, crit, item) in _rows)
        {
            var q = box.Text.Trim();
            if (q.Length == 0) continue;
            list.Add(new ChecklistItem { Id = item.Id, Question = q, IsCritical = crit.IsChecked == true });
        }
        EditedList = list;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}