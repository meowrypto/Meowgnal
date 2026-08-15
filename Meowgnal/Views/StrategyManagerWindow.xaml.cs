using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Meowgnal.Models;
using Meowgnal.Services;

namespace Meowgnal.Views;

public partial class StrategyManagerWindow : Window
{
    private readonly string _symbol;

    // Live drag state
    private StrategyDefinition? _draggedStrategy;
    private Border? _draggedCard;
    private double _grabOffsetY;
    private double _startPanelY;
    private double _dragTranslateY;
    private bool _isDragging;

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
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "⛶" : "❐";
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    public StrategyManagerWindow(string symbol)
    {
        InitializeComponent();
        _symbol = string.IsNullOrWhiteSpace(symbol) ? "BTC/USDT" : symbol;
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        ListPanel.Children.Clear();

        var strategies = StrategyStorageService.LoadAll()
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Assign sort orders if they are all zero (first-time migration).
        if (strategies.Count > 0 && strategies.All(s => s.SortOrder == 0))
        {
            for (int i = 0; i < strategies.Count; i++)
            {
                strategies[i].SortOrder = i;
                StrategyStorageService.Save(strategies[i]);
            }
        }

        if (strategies.Count == 0)
        {
            ListPanel.Children.Add(new TextBlock
            {
                Text = "No saved strategies yet. Use \"+ New strategy\" or \"Templates\" to create one.",
                Foreground = (Brush)FindResource("TextMuted"),
                Margin = new Thickness(6),
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var s in strategies)
        {
            var entryCount = s.EntryRules?.Conditions?.Count ?? 0;
            var exitCount = s.ExitRules?.Conditions?.Count ?? 0;

            var card = new Border
            {
                Background = (Brush)FindResource("BorderColor"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8),
                Tag = s
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Drag handle (⠿) on the left
            var handle = new TextBlock
            {
                Text = "⠿",
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.SizeAll,
                Tag = s
            };
            handle.PreviewMouseLeftButtonDown += Handle_PreviewMouseLeftButtonDown;
            handle.MouseMove += Handle_MouseMove;
            handle.MouseLeftButtonUp += Handle_MouseLeftButtonUp;
            Grid.SetColumn(handle, 0);
            grid.Children.Add(handle);

            var left = new StackPanel();
            left.Children.Add(new TextBlock
            {
                Text = s.Name,
                Foreground = (Brush)FindResource("TextPrimary"),
                FontSize = 14,
                FontWeight = FontWeights.Bold
            });
            left.Children.Add(new TextBlock
            {
                Text = $"{s.Symbol} · {s.Timeframe} · {s.DataSource}",
                Foreground = (Brush)FindResource("TextSecondary"),
                FontSize = 11,
                Margin = new Thickness(0, 3, 0, 0)
            });
            left.Children.Add(new TextBlock
            {
                Text = $"{entryCount} entry / {exitCount} exit conditions",
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            });

            var summaryLines = StrategyDescriptionService.Describe(s)
                .Split('\n')
                .Take(2)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();
            var summary = string.Join("  |  ", summaryLines);
            left.Children.Add(new TextBlock
            {
                Text = summary,
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 10,
                FontStyle = FontStyles.Italic,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxHeight = 30,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(left, 1);
            grid.Children.Add(left);

            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var editBtn = new Button { Content = "✏️ Edit", Style = (Style)FindResource("SecondaryButton"), Padding = new Thickness(10, 5, 10, 5), Tag = s, Margin = new Thickness(0, 0, 6, 0) };
            editBtn.Click += Edit_Click;
            right.Children.Add(editBtn);

            var copyBtn = new Button { Content = "📋 Copy", Style = (Style)FindResource("TertiaryButton"), Padding = new Thickness(10, 5, 10, 5), Tag = s, Margin = new Thickness(0, 0, 6, 0) };
            copyBtn.Click += Copy_Click;
            right.Children.Add(copyBtn);

            var exportBtn = new Button { Content = "📤 Export", Style = (Style)FindResource("TertiaryButton"), Padding = new Thickness(10, 5, 10, 5), Tag = s, Margin = new Thickness(0, 0, 6, 0) };
            exportBtn.Click += Export_Click;
            right.Children.Add(exportBtn);

            var deleteBtn = new Button { Content = "🗑 Delete", Style = (Style)FindResource("TertiaryButton"), Padding = new Thickness(10, 5, 10, 5), Tag = s, Foreground = (Brush)FindResource("Down") };
            deleteBtn.Click += Delete_Click;
            right.Children.Add(deleteBtn);

            Grid.SetColumn(right, 2);
            grid.Children.Add(right);

            card.Child = grid;
            ListPanel.Children.Add(card);
        }
    }

    #region Live drag & drop

    private void Handle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBlock handle || handle.Tag is not StrategyDefinition s) return;
        _draggedStrategy = s;
        _draggedCard = FindCardForStrategy(s);
        if (_draggedCard is null) return;

        _grabOffsetY = e.GetPosition(_draggedCard).Y;
        _startPanelY = e.GetPosition(ListPanel).Y;
        _dragTranslateY = 0;
        _isDragging = false;
        handle.CaptureMouse();
    }

    private void Handle_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedCard is null || e.LeftButton != MouseButtonState.Pressed) return;

        var posInPanel = e.GetPosition(ListPanel);

        if (!_isDragging)
        {
            // Small movement threshold so a plain click doesn't start a drag
            if (Math.Abs(posInPanel.Y - _startPanelY) < 6) return;

            _isDragging = true;
            Mouse.OverrideCursor = Cursors.SizeAll;
            Panel.SetZIndex(_draggedCard, 99);
            ElevateCard(_draggedCard);
        }

        // 1) Float the dragged card with the mouse
        var desiredTop = posInPanel.Y - _grabOffsetY;
        var slotTop = VisualTop(_draggedCard) - _dragTranslateY;
        _dragTranslateY = desiredTop - slotTop;
        SetTranslateY(_draggedCard, _dragTranslateY);

        // 2) Live-reorder the other cards with a smooth slide
        var centerY = desiredTop + _draggedCard.ActualHeight / 2;
        var target = ComputeTargetIndex(centerY);
        var current = ListPanel.Children.IndexOf(_draggedCard);
        if (target != current) ReorderWithAnimation(current, target);
    }

    private void Handle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBlock handle) handle.ReleaseMouseCapture();

        if (_isDragging && _draggedCard is not null)
        {
            // Persist the new order
            for (int i = 0; i < ListPanel.Children.Count; i++)
            {
                if (ListPanel.Children[i] is Border b && b.Tag is StrategyDefinition st && st.SortOrder != i)
                {
                    st.SortOrder = i;
                    StrategyStorageService.Save(st);
                }
            }

            // Settle the dragged card smoothly into its slot
            AnimateShiftFrom(_draggedCard, _dragTranslateY, TimeSpan.FromMilliseconds(160));
            ResetCardLift(_draggedCard);
            Panel.SetZIndex(_draggedCard, 0);
        }

        Mouse.OverrideCursor = null;
        _isDragging = false;
        _draggedCard = null;
        _draggedStrategy = null;
    }

    // Counts how many other cards sit above the dragged card's center.
    private int ComputeTargetIndex(double draggedCenterY)
    {
        int index = 0;
        foreach (var child in ListPanel.Children)
        {
            if (child is not Border b || b == _draggedCard) continue;
            var mid = VisualTop(b) + b.ActualHeight / 2;
            if (draggedCenterY > mid) index++;
        }
        return index;
    }

    // FLIP animation: record positions, move the child, then slide everyone to their new spot.
    private void ReorderWithAnimation(int fromIndex, int toIndex)
    {
        var oldTops = new System.Collections.Generic.Dictionary<Border, double>();
        foreach (var child in ListPanel.Children)
            if (child is Border b) oldTops[b] = VisualTop(b);

        var card = ListPanel.Children[fromIndex];
        ListPanel.Children.RemoveAt(fromIndex);
        ListPanel.Children.Insert(toIndex, card);
        ListPanel.UpdateLayout();

        foreach (var child in ListPanel.Children)
        {
            if (child is not Border b || b == _draggedCard) continue;
            var delta = oldTops[b] - VisualTop(b);
            if (Math.Abs(delta) < 1) continue;
            AnimateShiftFrom(b, delta, TimeSpan.FromMilliseconds(160));
        }
    }

    #endregion

    #region Transform / animation helpers

    private static TransformGroup EnsureGroup(Border card)
    {
        if (card.RenderTransform is TransformGroup g) return g;
        g = new TransformGroup();
        g.Children.Add(new ScaleTransform(1, 1));
        g.Children.Add(new TranslateTransform(0, 0));
        card.RenderTransform = g;
        return g;
    }

    private static ScaleTransform GetScale(Border card) =>
        EnsureGroup(card).Children.OfType<ScaleTransform>().First();

    private static TranslateTransform GetTranslate(Border card) =>
        EnsureGroup(card).Children.OfType<TranslateTransform>().First();

    private double VisualTop(Border card) =>
        card.TranslatePoint(new Point(0, 0), ListPanel).Y;

    private static void SetTranslateY(Border card, double y)
    {
        var t = GetTranslate(card);
        t.BeginAnimation(TranslateTransform.YProperty, null);
        t.Y = y;
    }

    // Starts the card at `fromY` and eases it back to 0 (used for the slide effect).
    private static void AnimateShiftFrom(Border card, double fromY, TimeSpan duration)
    {
        var t = GetTranslate(card);
        t.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(fromY, 0, duration) { EasingFunction = new QuadraticEase() });
    }

    // Lifted look while dragging: slight grow + deep shadow.
    private static void ElevateCard(Border card)
    {
        card.RenderTransformOrigin = new Point(0.5, 0.5);
        var s = GetScale(card);
        var anim = new DoubleAnimation(1.03, TimeSpan.FromMilliseconds(150)) { EasingFunction = new QuadraticEase() };
        s.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        s.BeginAnimation(ScaleTransform.ScaleYProperty, anim);

        card.Effect = new DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 24,
            ShadowDepth = 8,
            Opacity = 0.55,
            Direction = 270
        };
        card.Opacity = 0.96;
    }

    // Removes the lifted look (scale/shadow/opacity) without touching the translate.
    private static void ResetCardLift(Border card)
    {
        var s = GetScale(card);
        var anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150)) { EasingFunction = new QuadraticEase() };
        s.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        s.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        card.Effect = null;
        card.Opacity = 1.0;
    }

    private Border? FindCardForStrategy(StrategyDefinition s)
    {
        foreach (var child in ListPanel.Children)
            if (child is Border b && b.Tag is StrategyDefinition def && def.StrategyId == s.StrategyId)
                return b;
        return null;
    }

    #endregion

    private void NewStrategy_Click(object sender, RoutedEventArgs e)
    {
        new StrategyBuilderWindow { Owner = this }.ShowDialog();
        Refresh();
    }

    private void Templates_Click(object sender, RoutedEventArgs e)
    {
        var store = new TemplateStoreWindow(_symbol) { Owner = this };
        store.ShowDialog();
        Refresh();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not StrategyDefinition s) return;
        var builder = new StrategyBuilderWindow(s, StrategyBuilderWindow.BuilderOpenMode.Edit) { Owner = this };
        builder.ShowDialog();
        Refresh();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not StrategyDefinition s) return;
        var builder = new StrategyBuilderWindow(s, StrategyBuilderWindow.BuilderOpenMode.Copy) { Owner = this };
        builder.ShowDialog();
        Refresh();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not StrategyDefinition s) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export strategy",
            Filter = "Strategy file (*.mgstrat)|*.mgstrat|JSON file (*.json)|*.json",
            FileName = SanitizeFileName(s.Name) + ".mgstrat"
        };
        if (dialog.ShowDialog() != true) return;

        StrategyStorageService.Export(s, dialog.FileName);
        NotificationService.ShowToast("Meowgnal", $"Exported '{s.Name}'.");
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import strategy",
            Filter = "Strategy files (*.mgstrat;*.json)|*.mgstrat;*.json|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        var imported = StrategyStorageService.Import(dialog.FileName);
        if (imported is null)
        {
            MessageBox.Show("Could not read a strategy from this file.", "Meowgnal",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        NotificationService.ShowToast("Meowgnal", $"Imported '{imported.Name}'.");
        Refresh();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not StrategyDefinition s) return;

        var res = MessageBox.Show($"Are you sure you want to delete '{s.Name}'?", "Meowgnal",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        StrategyStorageService.Delete(s.StrategyId);
        NotificationService.ShowToast("Meowgnal", $"Deleted '{s.Name}'.");
        Refresh();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}