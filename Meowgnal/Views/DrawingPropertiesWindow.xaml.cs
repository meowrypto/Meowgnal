using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Meowgnal.Models;
using Drawing = Meowgnal.Models.Drawing;

namespace Meowgnal.Views;

public partial class DrawingPropertiesWindow : Window
{
    private readonly Drawing _drawing;
    private readonly List<TextBox> _priceBoxes = new();

    // Per-tool option checkboxes (built dynamically in BuildLineOptionsUi)
    private CheckBox? _extendLeft;
    private CheckBox? _extendRight;
    private CheckBox? _showPriceLabels;
    private CheckBox? _showTimeLabel;
    private CheckBox? _showPriceChange;
    private CheckBox? _showBarCount;
    private CheckBox? _showTimeElapsed;
    private CheckBox? _showAngle;
    // Channel-specific state
    private string _medianColor = "#FF9800";
    private string _secondLineColor = "";
    // Pitchfork-specific state
    private string _pfMedianColor = "#FF9800";
    private string _pfArm1Color = "#2962FF";
    private string _pfArm2Color = "#2962FF";

    public DrawingPropertiesWindow(Drawing drawing)
    {
        InitializeComponent();
        _drawing = drawing;

        LabelBox.Text = drawing.Label;
        HexBox.Text = drawing.Color;
        AlertCheck.IsChecked = drawing.AlertOnCross;
        LockedCheck.IsChecked = drawing.IsLocked;
        HiddenCheck.IsChecked = !drawing.IsVisible;
        UpdateSwatch();

        // Thickness options (1-4 pixels, TradingView standard)
        for (var w = 1; w <= 4; w++) WidthCombo.Items.Add(w);
        WidthCombo.SelectedItem = drawing.LineWidth is >= 1 and <= 4 ? drawing.LineWidth : 2;

        // Line style options (TradingView standard)
        StyleCombo.Items.Add("solid");
        StyleCombo.Items.Add("dashed");
        StyleCombo.Items.Add("dotted");
        StyleCombo.SelectedItem = drawing.LineStyle;

        // Font family options for text drawings
        var fonts = new[] { "Trebuchet MS", "Arial", "Courier New", "Georgia", "Verdana", "Times New Roman", "Segoe UI" };
        foreach (var f in fonts) FontCombo.Items.Add(f);
        FontCombo.SelectedItem = string.IsNullOrEmpty(drawing.FontFamily) ? "Trebuchet MS" : drawing.FontFamily;

        // Font size options
        foreach (var s in new[] { 10, 11, 12, 13, 14, 16, 18, 20, 24, 28, 32, 36, 48, 64 })
            FontSizeCombo.Items.Add(s);
        FontSizeCombo.SelectedItem = drawing.FontSize is >= 8 and <= 100 ? drawing.FontSize : 13;

        // Gann ratios: default to standard Gann angles
        GannRatiosBox.Text = drawing.GannRatios is not null
            ? string.Join(", ", drawing.GannRatios.Select(r => r.ToString("0.###")))
            : "0.125, 0.25, 0.333, 0.5, 1, 2, 3, 4, 8";

        // Gann Square Fixed settings
        if (drawing.Kind == DrawingKind.GannSquareFixed)
        {
            GannSquareFixedSection.Visibility = Visibility.Visible;
            for (int i = 2; i <= 10; i++) GannSquareDivisionsCombo.Items.Add(i);
            GannSquareDivisionsCombo.SelectedItem = drawing.GannSquareDivisions is >= 2 and <= 10 ? drawing.GannSquareDivisions : 4;
        }

        // Gann Box settings
        if (drawing.Kind == DrawingKind.GannBox)
        {
            GannBoxSection.Visibility = Visibility.Visible;
            var defaultColor = string.IsNullOrEmpty(drawing.Color) ? "#2962FF" : drawing.Color;
            var sourceLevels = drawing.FibLevels;
            if (sourceLevels == null || sourceLevels.Count == 0)
            {
                sourceLevels = FibonacciDefaults.GetDefaultRetracementLevels(defaultColor);
            }
            GannBoxLevelsEditor.Levels = new System.Collections.ObjectModel.ObservableCollection<FibLevel>(sourceLevels);
        }
        // Channel settings initialization
        _medianColor = drawing.MedianLineColor;
        _secondLineColor = drawing.SecondLineColor;
        var isChannel = drawing.Kind is DrawingKind.ParallelChannel or DrawingKind.RegressionTrend
            or DrawingKind.FlatTopBottom or DrawingKind.DisjointChannel;
        if (isChannel)
        {
            ChannelSection.Visibility = Visibility.Visible;
            FillBgCheck.IsChecked = drawing.FillBackground;
            FillOpacitySlider.Value = drawing.FillOpacity;
            FillOpacityLabel.Text = $"{(int)(drawing.FillOpacity * 100)}%";
            FillOpacityPanel.Visibility = drawing.FillBackground ? Visibility.Visible : Visibility.Collapsed;
            FillOpacitySlider.ValueChanged += (_, _) => FillOpacityLabel.Text = $"{(int)(FillOpacitySlider.Value * 100)}%";
            if (drawing.Kind == DrawingKind.ParallelChannel)
            {
                MedianSection.Visibility = Visibility.Visible;
                MedianCheck.IsChecked = drawing.ShowMedianLine;
            }
            if (drawing.Kind == DrawingKind.RegressionTrend)
            {
                StdDevSection.Visibility = Visibility.Visible;
                StdDevCombo.Items.Add(1);
                StdDevCombo.Items.Add(2);
                StdDevCombo.Items.Add(3);
                StdDevCombo.SelectedItem = drawing.StdDevMultiplier is >= 1 and <= 3 ? drawing.StdDevMultiplier : 2;
            }
            if (drawing.Kind == DrawingKind.DisjointChannel)
            {
                SecondColorSection.Visibility = Visibility.Visible;
                UpdateSecondColorPreview();
            }
        }
        // Pitchfork settings initialization
        var isPitchfork = drawing.Kind is DrawingKind.Pitchfork or DrawingKind.SchiffPitchfork
            or DrawingKind.ModifiedSchiffPitchfork or DrawingKind.InsidePitchfork;
        if (isPitchfork)
        {
            PitchforkSection.Visibility = Visibility.Visible;
            _pfMedianColor = drawing.PitchforkMedianColor;
            _pfArm1Color = drawing.PitchforkArm1Color;
            _pfArm2Color = drawing.PitchforkArm2Color;
            PfSameColorCheck.IsChecked = drawing.PitchforkUseSameColor;
            PfSeparateColorsPanel.Visibility = drawing.PitchforkUseSameColor ? Visibility.Collapsed : Visibility.Visible;
            PfExtendRightCheck.IsChecked = drawing.ExtendRight;
        }
        // Show the right section based on drawing kind
        if (drawing.Kind is DrawingKind.Text or DrawingKind.Note or DrawingKind.Sticker)
        {
            TextSection.Visibility = Visibility.Visible;
        }
        else if (drawing.Kind == DrawingKind.GannFan)
        {
            GannSection.Visibility = Visibility.Visible;
        }

        // Build per-tool display option checkboxes (inserted into OptionsPanel)
        BuildLineOptionsUi(drawing);

        // Initialize Fibonacci levels editor if this is a Fibonacci tool
        var isFib = drawing.Kind is DrawingKind.Fibonacci or DrawingKind.FibExtension
    or DrawingKind.FibChannel or DrawingKind.FibTimeZone or DrawingKind.TrendBasedFibTime
    or DrawingKind.FibCircles or DrawingKind.FibSpiral or DrawingKind.FibArcs
    or DrawingKind.FibWedge or DrawingKind.FibSpeedFan or DrawingKind.Pitchfan;

        if (isFib)
        {
            FibSection.Visibility = Visibility.Visible;
            var defaultColor = string.IsNullOrEmpty(drawing.Color) ? "#2962FF" : drawing.Color;
            var sourceLevels = drawing.FibLevels;
            if (sourceLevels == null || sourceLevels.Count == 0)
            {
                sourceLevels = drawing.Kind == DrawingKind.FibExtension
                    ? FibonacciDefaults.GetDefaultExtensionLevels(defaultColor)
                    : FibonacciDefaults.GetDefaultRetracementLevels(defaultColor);
            }
            FibLevelsEditor.Levels = new System.Collections.ObjectModel.ObservableCollection<FibLevel>(sourceLevels);
        }

        // Build coordinate rows: time is read-only, price is editable
        for (var i = 0; i < drawing.Points.Count; i++)
        {
            var p = drawing.Points[i];
            var row = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10, GridUnitType.Star) });

            var header = new TextBlock
            {
                Text = $"P{i + 1}",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("TextColor")
            };
            Grid.SetColumn(header, 0);
            row.Children.Add(header);

            var time = new TextBlock
            {
                Text = System.DateTimeOffset.FromUnixTimeSeconds(p.TimeUnix).UtcDateTime.ToString("yyyy/MM/dd HH:mm"),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("TextMuted"),
                FontSize = 11
            };
            Grid.SetColumn(time, 1);
            row.Children.Add(time);

            var priceBox = new TextBox
            {
                Text = p.Price.ToString(CultureInfo.InvariantCulture),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 120
            };
            Grid.SetColumn(priceBox, 2);
            row.Children.Add(priceBox);
            _priceBoxes.Add(priceBox);

            PointsPanel.Children.Add(row);
        }
    }

    /// <summary>
    /// Builds per-tool display option checkboxes based on the drawing kind.
    /// Each checkbox maps to a boolean property on the Drawing model.
    /// </summary>
    private void BuildLineOptionsUi(Drawing drawing)
    {
        // Determine which options apply to this drawing kind
        var needsExtendLeft = drawing.Kind is DrawingKind.TrendLine or DrawingKind.Ray or DrawingKind.ExtendedLine;
        var needsExtendRight = drawing.Kind is DrawingKind.TrendLine;
        var needsPriceLabel = drawing.Kind is DrawingKind.TrendLine or DrawingKind.HorizontalLine
            or DrawingKind.HorizontalRay or DrawingKind.Crossline or DrawingKind.Ray or DrawingKind.ExtendedLine;
        var needsTimeLabel = drawing.Kind is DrawingKind.VerticalLine or DrawingKind.Crossline;
        var needsPriceChange = drawing.Kind == DrawingKind.InfoLine;
        var needsBarCount = drawing.Kind == DrawingKind.InfoLine;
        var needsTimeElapsed = drawing.Kind == DrawingKind.InfoLine;
        var needsAngle = drawing.Kind is DrawingKind.InfoLine or DrawingKind.TrendAngle;

        // If nothing applies, hide the whole options section
        if (!needsExtendLeft && !needsExtendRight && !needsPriceLabel && !needsTimeLabel
            && !needsPriceChange && !needsBarCount && !needsTimeElapsed && !needsAngle)
        {
            if (FindName("OptionsPanel") is StackPanel panel)
                panel.Visibility = Visibility.Collapsed;
            return;
        }

        if (FindName("OptionsPanel") is not StackPanel optionsPanel) return;
        optionsPanel.Visibility = Visibility.Visible;

        // Header
        optionsPanel.Children.Add(new TextBlock
        {
            Text = "Display options",
            Foreground = (Brush)FindResource("TextColor"),
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 4)
        });

        if (needsExtendLeft)
        {
            _extendLeft = MakeOptionCheckBox("Extend left", drawing.ExtendLeft);
            optionsPanel.Children.Add(_extendLeft);
        }

        if (needsExtendRight)
        {
            _extendRight = MakeOptionCheckBox("Extend right", drawing.ExtendRight);
            optionsPanel.Children.Add(_extendRight);
        }

        if (needsPriceLabel)
        {
            _showPriceLabels = MakeOptionCheckBox("Show price label", drawing.ShowPriceLabels);
            optionsPanel.Children.Add(_showPriceLabels);
        }

        if (needsTimeLabel)
        {
            _showTimeLabel = MakeOptionCheckBox("Show time label", drawing.ShowTimeLabel);
            optionsPanel.Children.Add(_showTimeLabel);
        }

        if (needsPriceChange)
        {
            _showPriceChange = MakeOptionCheckBox("Show price change %", drawing.ShowPriceChange);
            optionsPanel.Children.Add(_showPriceChange);
        }

        if (needsBarCount)
        {
            _showBarCount = MakeOptionCheckBox("Show bar count", drawing.ShowBarCount);
            optionsPanel.Children.Add(_showBarCount);
        }

        if (needsTimeElapsed)
        {
            _showTimeElapsed = MakeOptionCheckBox("Show time elapsed", drawing.ShowTimeElapsed);
            optionsPanel.Children.Add(_showTimeElapsed);
        }

        if (needsAngle)
        {
            _showAngle = MakeOptionCheckBox("Show angle", drawing.ShowAngle);
            optionsPanel.Children.Add(_showAngle);
        }
    }

    /// <summary>Creates a styled checkbox consistent with the rest of the window.</summary>
    private CheckBox MakeOptionCheckBox(string label, bool isChecked)
    {
        return new CheckBox
        {
            Content = label,
            IsChecked = isChecked,
            Foreground = (Brush)FindResource("TextColor"),
            Margin = new Thickness(0, 2, 0, 2)
        };
    }

    #region Custom title bar

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

    #endregion

    private void UpdateSwatch()
    {
        try { PreviewRect.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(HexBox.Text)); }
        catch { PreviewRect.Fill = new SolidColorBrush(Colors.Gray); }
    }

    private void Swatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string hex) return;
        HexBox.Text = hex;
        UpdateSwatch();
    }

    private void HexBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateSwatch();
    private void FillBgCheck_Changed(object sender, RoutedEventArgs e)
    {
        FillOpacityPanel.Visibility = FillBgCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }
    private void MedianColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string hex) return;
        _medianColor = hex;
    }
    private void SecondColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string hex) return;
        _secondLineColor = hex;
        UpdateSecondColorPreview();
    }
    private void UpdateSecondColorPreview()
    {
        try
        {
            SecondColorPreview.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
            string.IsNullOrEmpty(_secondLineColor) ? HexBox.Text : _secondLineColor));
        }
        catch { SecondColorPreview.Fill = new SolidColorBrush(Colors.Gray); }
    }
    private void PfSameColor_Changed(object sender, RoutedEventArgs e)
    {
        PfSeparateColorsPanel.Visibility = PfSameColorCheck.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
    }
    private void PfMedianColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string hex) return;
        _pfMedianColor = hex;
    }
    private void PfArm1Color_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string hex) return;
        _pfArm1Color = hex;
    }
    private void PfArm2Color_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string hex) return;
        _pfArm2Color = hex;
    }
    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _drawing.Label = LabelBox.Text.Trim();
        _drawing.Color = HexBox.Text.Trim();
        _drawing.AlertOnCross = AlertCheck.IsChecked == true;
        _drawing.IsLocked = LockedCheck.IsChecked == true;
        _drawing.IsVisible = HiddenCheck.IsChecked != true;

        // Save thickness and line style
        _drawing.LineWidth = WidthCombo.SelectedItem is int w ? w : 2;
        _drawing.LineStyle = StyleCombo.SelectedItem as string ?? "solid";

        // Save font and size for text drawings
        if (_drawing.Kind is DrawingKind.Text or DrawingKind.Note or DrawingKind.Sticker)
        {
            _drawing.FontFamily = FontCombo.SelectedItem as string ?? "Trebuchet MS";
            _drawing.FontSize = FontSizeCombo.SelectedItem is int s ? s : 13;
        }

        // Save Gann Square Fixed settings
        if (_drawing.Kind == DrawingKind.GannSquareFixed)
        {
            _drawing.GannSquareDivisions = GannSquareDivisionsCombo.SelectedItem is int div ? div : 4;
        }

        // Save Gann Box levels
        if (_drawing.Kind == DrawingKind.GannBox && GannBoxLevelsEditor.Levels != null)
        {
            _drawing.FibLevels = new List<FibLevel>(GannBoxLevelsEditor.Levels);
        }

        // Save Gann ratios
        if (_drawing.Kind == DrawingKind.GannFan)
        {
            var ratios = new List<double>();
            foreach (var part in GannRatiosBox.Text.Split(','))
            {
                if (double.TryParse(part.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var r) && r > 0)
                    ratios.Add(r);
            }
            _drawing.GannRatios = ratios.Count > 0 ? ratios : null;
        }
        // Save channel settings
        var isChannelKind = _drawing.Kind is DrawingKind.ParallelChannel or DrawingKind.RegressionTrend
            or DrawingKind.FlatTopBottom or DrawingKind.DisjointChannel;
        if (isChannelKind)
        {
            _drawing.FillBackground = FillBgCheck.IsChecked == true;
            _drawing.FillOpacity = FillOpacitySlider.Value;
            if (_drawing.Kind == DrawingKind.ParallelChannel)
            {
                _drawing.ShowMedianLine = MedianCheck.IsChecked == true;
                _drawing.MedianLineColor = _medianColor;
            }
            if (_drawing.Kind == DrawingKind.RegressionTrend)
                _drawing.StdDevMultiplier = StdDevCombo.SelectedItem is int sd ? sd : 2;
            if (_drawing.Kind == DrawingKind.DisjointChannel)
                _drawing.SecondLineColor = _secondLineColor;
        }
        // Save pitchfork settings
        var isPfKind = _drawing.Kind is DrawingKind.Pitchfork or DrawingKind.SchiffPitchfork
            or DrawingKind.ModifiedSchiffPitchfork or DrawingKind.InsidePitchfork;
        if (isPfKind)
        {
            _drawing.PitchforkUseSameColor = PfSameColorCheck.IsChecked == true;
            _drawing.PitchforkMedianColor = _pfMedianColor;
            _drawing.PitchforkArm1Color = _pfArm1Color;
            _drawing.PitchforkArm2Color = _pfArm2Color;
            _drawing.ExtendRight = PfExtendRightCheck.IsChecked == true;
        }
        // Save Fibonacci levels
        if (FibSection.Visibility == Visibility.Visible && FibLevelsEditor.Levels != null)
        {
            _drawing.FibLevels = new List<FibLevel>(FibLevelsEditor.Levels);
        }

        // Save per-tool display options
        if (_extendLeft is not null) _drawing.ExtendLeft = _extendLeft.IsChecked == true;
        if (_extendRight is not null) _drawing.ExtendRight = _extendRight.IsChecked == true;
        if (_showPriceLabels is not null) _drawing.ShowPriceLabels = _showPriceLabels.IsChecked == true;
        if (_showTimeLabel is not null) _drawing.ShowTimeLabel = _showTimeLabel.IsChecked == true;
        if (_showPriceChange is not null) _drawing.ShowPriceChange = _showPriceChange.IsChecked == true;
        if (_showBarCount is not null) _drawing.ShowBarCount = _showBarCount.IsChecked == true;
        if (_showTimeElapsed is not null) _drawing.ShowTimeElapsed = _showTimeElapsed.IsChecked == true;
        if (_showAngle is not null) _drawing.ShowAngle = _showAngle.IsChecked == true;

        // Save edited prices
        for (var i = 0; i < _drawing.Points.Count && i < _priceBoxes.Count; i++)
        {
            if (decimal.TryParse(_priceBoxes[i].Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) && price > 0)
                _drawing.Points[i].Price = price;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}