using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

    // Pattern-specific state
    private string _necklineColor = "#FF9800";

    // Elliott-specific state
    private string _elliottLabelColor = "";

    // Forecast Position state
    private string _profitZoneColor = "#089981";
    private string _lossZoneColor = "#F23645";

    // Cycles-specific state
    private int _cycleCount = 10;
    private long _cycleIntervalSeconds = 0;
    private double _sineAmplitudePercent = 50;
    private int _sineRepeatCount = 3;
    // Volume profile state
    private string _profileColor = "#2962FF";

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
        else if (drawing.Kind is DrawingKind.XabcdPattern or DrawingKind.CypherPattern or DrawingKind.HeadAndShoulders
        or DrawingKind.AbcdPattern or DrawingKind.TrianglePattern or DrawingKind.ThreeDrivesPattern)
        {
            PatternSection.Visibility = Visibility.Visible;
            ShowRatiosCheck.IsChecked = drawing.ShowRatios;
            ShowLabelsCheck.IsChecked = drawing.ShowLabels;
            ShowApexCheck.IsChecked = drawing.ShowApex;
            _necklineColor = drawing.NecklineColor;
        }

        var isElliottKind = drawing.Kind is DrawingKind.ElliottImpulseWave or DrawingKind.ElliottCorrectionWave
        or DrawingKind.ElliottTriangleWave or DrawingKind.ElliottDoubleComboWave
        or DrawingKind.ElliottTripleComboWave;
        if (isElliottKind)
        {
            ElliottSection.Visibility = Visibility.Visible;
            ElliottShowLabelsCheck.IsChecked = drawing.ShowLabels;
            _elliottLabelColor = drawing.LabelColor;
            UpdateElliottLabelColorPreview();
        }

        var isCyclesKind = drawing.Kind is DrawingKind.CyclicLines or DrawingKind.TimeCycles;
        if (isCyclesKind)
        {
            CyclesSection.Visibility = Visibility.Visible;
            CycleShowLabelsCheck.IsChecked = drawing.ShowLabels;
            _cycleCount = drawing.CycleCount;
            _cycleIntervalSeconds = drawing.CycleIntervalSeconds;
            for (int i = 1; i <= 50; i++) CycleCountCombo.Items.Add(i);
            CycleCountCombo.SelectedItem = drawing.CycleCount is >= 1 and <= 50 ? drawing.CycleCount : 10;
            CycleIntervalBox.Text = drawing.CycleIntervalSeconds > 0 ? drawing.CycleIntervalSeconds.ToString() : "";
        }

        if (drawing.Kind == DrawingKind.SineLine)
        {
            SineSection.Visibility = Visibility.Visible;
            _sineAmplitudePercent = drawing.SineAmplitudePercent;
            _sineRepeatCount = drawing.SineRepeatCount;
            for (int i = 10; i <= 200; i += 10) SineAmplitudeCombo.Items.Add(i);
            SineAmplitudeCombo.SelectedItem = drawing.SineAmplitudePercent is >= 10 and <= 200 ? (int)drawing.SineAmplitudePercent : 50;
            for (int i = 1; i <= 10; i++) SineRepeatCombo.Items.Add(i);
            SineRepeatCombo.SelectedItem = drawing.SineRepeatCount is >= 1 and <= 10 ? drawing.SineRepeatCount : 3;
        }

        var isForecastPositionKind = drawing.Kind is DrawingKind.LongPosition or DrawingKind.ShortPosition;
        if (isForecastPositionKind)
        {
            ForecastPositionSection.Visibility = Visibility.Visible;
            EntryPriceBox.Text = drawing.EntryPrice.ToString();
            StopLossPriceBox.Text = drawing.StopLossPrice.ToString();
            TakeProfitPriceBox.Text = drawing.TakeProfitPrice.ToString();
            PositionSizeBox.Text = drawing.PositionSizePercent.ToString();
            _profitZoneColor = drawing.ProfitZoneColor;
            _lossZoneColor = drawing.LossZoneColor;
            UpdateProfitZoneColorPreview();
            UpdateLossZoneColorPreview();
            UpdateRiskReward();
            EntryPriceBox.TextChanged += PriceBox_TextChanged;
            StopLossPriceBox.TextChanged += PriceBox_TextChanged;
            TakeProfitPriceBox.TextChanged += PriceBox_TextChanged;
        }

        if (drawing.Kind == DrawingKind.GhostFeed)
        {
            GhostFeedSection.Visibility = Visibility.Visible;
            if (Application.Current.MainWindow is MainWindow mw)
            {
                foreach (var sym in mw.GetWatchlistSymbols())
                    GhostSymbolCombo.Items.Add(sym);
            }
            GhostSymbolBox.Text = drawing.GhostSymbol;
            GhostOpacitySlider.Value = drawing.GhostOpacity;
        }

        if (drawing.Kind is DrawingKind.Sector or DrawingKind.BarsPattern)
        {
            SectorSection.Visibility = Visibility.Visible;
            SectorOpacitySlider.Value = drawing.Kind == DrawingKind.Sector ? drawing.SectorFillOpacity : drawing.BarsPatternOpacity;
        }
        if (drawing.Kind == DrawingKind.AnchoredVwap)
        {
            VwapSection.Visibility = Visibility.Visible;
            ShowVwapBandsCheck.IsChecked = drawing.ShowVwapBands;
        }
        if (drawing.Kind is DrawingKind.FixedRangeVolumeProfile or DrawingKind.AnchoredVolumeProfile)
        {
            VolumeProfileSection.Visibility = Visibility.Visible;
            for (int i = 8; i <= 48; i += 4) BucketCountCombo.Items.Add(i);
            BucketCountCombo.SelectedItem = drawing.VolumeBucketCount is >= 8 and <= 48 ? drawing.VolumeBucketCount : 24;
            ProfileWidthSlider.Value = drawing.VolumeProfileWidthPercent;
            _profileColor = drawing.VolumeProfileColor;
            UpdateProfileColorPreview();
        }
        if (drawing.Kind is DrawingKind.PriceRange or DrawingKind.DateRange or DrawingKind.DateAndPriceRange)
        {
            MeasureSection.Visibility = Visibility.Visible;
            var isPriceKind = drawing.Kind is DrawingKind.PriceRange or DrawingKind.DateAndPriceRange;
            var isTimeKind = drawing.Kind is DrawingKind.DateRange or DrawingKind.DateAndPriceRange;
            PriceModeLabel.Visibility = PriceModeCombo.Visibility = isPriceKind ? Visibility.Visible : Visibility.Collapsed;
            TimeUnitLabel.Visibility = TimeUnitCombo.Visibility = isTimeKind ? Visibility.Visible : Visibility.Collapsed;
            PriceModeCombo.Items.Add("both");
            PriceModeCombo.Items.Add("absolute");
            PriceModeCombo.Items.Add("percent");
            PriceModeCombo.SelectedItem = drawing.PriceRangeMode is "absolute" or "percent" ? drawing.PriceRangeMode : "both";
            TimeUnitCombo.Items.Add("days");
            TimeUnitCombo.Items.Add("hours");
            TimeUnitCombo.Items.Add("bars");
            TimeUnitCombo.SelectedItem = drawing.DateRangeUnit is "hours" or "bars" ? drawing.DateRangeUnit : "days";
        }

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

        // Build per-tool display option checkboxes (inserted into OptionsPanel)
        BuildLineOptionsUi(drawing);

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
    /// </summary>
    private void BuildLineOptionsUi(Drawing drawing)
    {
        var needsExtendLeft = drawing.Kind is DrawingKind.TrendLine or DrawingKind.Ray or DrawingKind.ExtendedLine;
        var needsExtendRight = drawing.Kind is DrawingKind.TrendLine;
        var needsPriceLabel = drawing.Kind is DrawingKind.TrendLine or DrawingKind.HorizontalLine
        or DrawingKind.HorizontalRay or DrawingKind.Crossline or DrawingKind.Ray or DrawingKind.ExtendedLine;
        var needsTimeLabel = drawing.Kind is DrawingKind.VerticalLine or DrawingKind.Crossline;
        var needsPriceChange = drawing.Kind == DrawingKind.InfoLine;
        var needsBarCount = drawing.Kind == DrawingKind.InfoLine;
        var needsTimeElapsed = drawing.Kind == DrawingKind.InfoLine;
        var needsAngle = drawing.Kind is DrawingKind.InfoLine or DrawingKind.TrendAngle;
        if (!needsExtendLeft && !needsExtendRight && !needsPriceLabel && !needsTimeLabel
        && !needsPriceChange && !needsBarCount && !needsTimeElapsed && !needsAngle)
        {
            if (FindName("OptionsPanel") is StackPanel panel)
                panel.Visibility = Visibility.Collapsed;
            return;
        }
        if (FindName("OptionsPanel") is not StackPanel optionsPanel) return;
        optionsPanel.Visibility = Visibility.Visible;
        optionsPanel.Children.Add(new TextBlock
        {
            Text = "Display options",
            Foreground = (Brush)FindResource("TextColor"),
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 4)
        });
        if (needsExtendLeft) { _extendLeft = MakeOptionCheckBox("Extend left", drawing.ExtendLeft); optionsPanel.Children.Add(_extendLeft); }
        if (needsExtendRight) { _extendRight = MakeOptionCheckBox("Extend right", drawing.ExtendRight); optionsPanel.Children.Add(_extendRight); }
        if (needsPriceLabel) { _showPriceLabels = MakeOptionCheckBox("Show price label", drawing.ShowPriceLabels); optionsPanel.Children.Add(_showPriceLabels); }
        if (needsTimeLabel) { _showTimeLabel = MakeOptionCheckBox("Show time label", drawing.ShowTimeLabel); optionsPanel.Children.Add(_showTimeLabel); }
        if (needsPriceChange) { _showPriceChange = MakeOptionCheckBox("Show price change %", drawing.ShowPriceChange); optionsPanel.Children.Add(_showPriceChange); }
        if (needsBarCount) { _showBarCount = MakeOptionCheckBox("Show bar count", drawing.ShowBarCount); optionsPanel.Children.Add(_showBarCount); }
        if (needsTimeElapsed) { _showTimeElapsed = MakeOptionCheckBox("Show time elapsed", drawing.ShowTimeElapsed); optionsPanel.Children.Add(_showTimeElapsed); }
        if (needsAngle) { _showAngle = MakeOptionCheckBox("Show angle", drawing.ShowAngle); optionsPanel.Children.Add(_showAngle); }
    }

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

    private void NecklineColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string hex) return;
        _necklineColor = hex;
    }

    private void ElliottLabelColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string hex) return;
        _elliottLabelColor = hex;
        UpdateElliottLabelColorPreview();
    }

    private void ElliottLabelColorAuto_Click(object sender, RoutedEventArgs e)
    {
        _elliottLabelColor = "";
        UpdateElliottLabelColorPreview();
    }

    private void PriceBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateRiskReward();
    }

    private void UpdateRiskReward()
    {
        if (!decimal.TryParse(EntryPriceBox.Text, out var entry) || !decimal.TryParse(StopLossPriceBox.Text, out var sl) || !decimal.TryParse(TakeProfitPriceBox.Text, out var tp))
        {
            RiskRewardText.Text = "—";
            return;
        }
        var risk = Math.Abs(entry - sl);
        var reward = Math.Abs(tp - entry);
        if (risk == 0) { RiskRewardText.Text = "—"; return; }
        var rr = reward / risk;
        RiskRewardText.Text = $"1 : {rr:F2}";
    }

    private void ProfitZoneColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string hex) return;
        _profitZoneColor = hex;
        UpdateProfitZoneColorPreview();
    }

    private void LossZoneColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string hex) return;
        _lossZoneColor = hex;
        UpdateLossZoneColorPreview();
    }

    private void UpdateProfitZoneColorPreview()
    {
        try { ProfitZoneColorPreview.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_profitZoneColor)); }
        catch { ProfitZoneColorPreview.Fill = new SolidColorBrush(Colors.Green); }
    }

    private void UpdateLossZoneColorPreview()
    {
        try { LossZoneColorPreview.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_lossZoneColor)); }
        catch { LossZoneColorPreview.Fill = new SolidColorBrush(Colors.Red); }
    }
    private void ProfileColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string hex) return;
        _profileColor = hex;
        UpdateProfileColorPreview();
    }
    private void UpdateProfileColorPreview()
    {
        try { ProfileColorPreview.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_profileColor)); }
        catch { ProfileColorPreview.Fill = new SolidColorBrush(Colors.Blue); }
    }

    private async void OpenAsPaper_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(EntryPriceBox.Text, out var entry) || !decimal.TryParse(StopLossPriceBox.Text, out var sl) || !decimal.TryParse(TakeProfitPriceBox.Text, out var tp) || !decimal.TryParse(PositionSizeBox.Text, out var size))
        {
            MessageBox.Show("Please fill in all price fields correctly.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (Application.Current.MainWindow is MainWindow mw)
        {
            var side = _drawing.Kind == DrawingKind.LongPosition ? "long" : "short";
            var result = await mw.OpenPaperPositionFromDrawingAsync(side, entry, sl, tp, size);
            if (result)
            {
                MessageBox.Show("Position opened successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
        }
    }

    private void UpdateElliottLabelColorPreview()
    {
        try
        {
            ElliottLabelColorPreview.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
            string.IsNullOrEmpty(_elliottLabelColor) ? HexBox.Text : _elliottLabelColor));
        }
        catch { ElliottLabelColorPreview.Fill = new SolidColorBrush(Colors.Gray); }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _drawing.Label = LabelBox.Text.Trim();
        _drawing.Color = HexBox.Text.Trim();
        _drawing.AlertOnCross = AlertCheck.IsChecked == true;
        _drawing.IsLocked = LockedCheck.IsChecked == true;
        _drawing.IsVisible = HiddenCheck.IsChecked != true;
        _drawing.LineWidth = WidthCombo.SelectedItem is int w ? w : 2;
        _drawing.LineStyle = StyleCombo.SelectedItem as string ?? "solid";

        if (_drawing.Kind is DrawingKind.Text or DrawingKind.Note or DrawingKind.Sticker)
        {
            _drawing.FontFamily = FontCombo.SelectedItem as string ?? "Trebuchet MS";
            _drawing.FontSize = FontSizeCombo.SelectedItem is int s ? s : 13;
        }

        if (_drawing.Kind == DrawingKind.GannSquareFixed)
        {
            _drawing.GannSquareDivisions = GannSquareDivisionsCombo.SelectedItem is int div ? div : 4;
        }

        if (_drawing.Kind == DrawingKind.GannBox && GannBoxLevelsEditor.Levels != null)
        {
            _drawing.FibLevels = new List<FibLevel>(GannBoxLevelsEditor.Levels);
        }

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

        var isPatternKind = _drawing.Kind is DrawingKind.XabcdPattern or DrawingKind.CypherPattern or DrawingKind.HeadAndShoulders
        or DrawingKind.AbcdPattern or DrawingKind.TrianglePattern or DrawingKind.ThreeDrivesPattern;
        if (isPatternKind)
        {
            _drawing.ShowRatios = ShowRatiosCheck.IsChecked == true;
            _drawing.ShowLabels = ShowLabelsCheck.IsChecked == true;
            _drawing.ShowApex = ShowApexCheck.IsChecked == true;
            _drawing.NecklineColor = _necklineColor;
        }

        var isElliottKind = _drawing.Kind is DrawingKind.ElliottImpulseWave or DrawingKind.ElliottCorrectionWave
        or DrawingKind.ElliottTriangleWave or DrawingKind.ElliottDoubleComboWave
        or DrawingKind.ElliottTripleComboWave;
        if (isElliottKind)
        {
            _drawing.ShowLabels = ElliottShowLabelsCheck.IsChecked == true;
            _drawing.LabelColor = _elliottLabelColor;
        }

        var isCyclesKind = _drawing.Kind is DrawingKind.CyclicLines or DrawingKind.TimeCycles;
        if (isCyclesKind)
        {
            _drawing.ShowLabels = CycleShowLabelsCheck.IsChecked == true;
            _drawing.CycleCount = CycleCountCombo.SelectedItem is int cc ? cc : 10;
            if (long.TryParse(CycleIntervalBox.Text, out var interval) && interval > 0)
                _drawing.CycleIntervalSeconds = interval;
            else
                _drawing.CycleIntervalSeconds = 0;
        }

        if (_drawing.Kind == DrawingKind.SineLine)
        {
            _drawing.SineAmplitudePercent = SineAmplitudeCombo.SelectedItem is int sa ? sa : 50;
            _drawing.SineRepeatCount = SineRepeatCombo.SelectedItem is int sr ? sr : 3;
        }

        var isForecastPositionKind = _drawing.Kind is DrawingKind.LongPosition or DrawingKind.ShortPosition;
        if (isForecastPositionKind)
        {
            if (decimal.TryParse(EntryPriceBox.Text, out var entry)) _drawing.EntryPrice = entry;
            if (decimal.TryParse(StopLossPriceBox.Text, out var sl)) _drawing.StopLossPrice = sl;
            if (decimal.TryParse(TakeProfitPriceBox.Text, out var tp)) _drawing.TakeProfitPrice = tp;
            if (decimal.TryParse(PositionSizeBox.Text, out var size)) _drawing.PositionSizePercent = Math.Max(0, Math.Min(100, size));
            _drawing.ProfitZoneColor = _profitZoneColor;
            _drawing.LossZoneColor = _lossZoneColor;
        }

        if (_drawing.Kind == DrawingKind.GhostFeed)
        {
            _drawing.GhostSymbol = string.IsNullOrWhiteSpace(GhostSymbolBox.Text) ? GhostSymbolCombo.SelectedItem?.ToString() ?? "" : GhostSymbolBox.Text;
            _drawing.GhostOpacity = GhostOpacitySlider.Value;
        }

        if (_drawing.Kind is DrawingKind.Sector or DrawingKind.BarsPattern)
        {
            if (_drawing.Kind == DrawingKind.Sector)
                _drawing.SectorFillOpacity = SectorOpacitySlider.Value;
            else
                _drawing.BarsPatternOpacity = SectorOpacitySlider.Value;
        }
        if (_drawing.Kind == DrawingKind.AnchoredVwap)
            _drawing.ShowVwapBands = ShowVwapBandsCheck.IsChecked == true;
        if (_drawing.Kind is DrawingKind.FixedRangeVolumeProfile or DrawingKind.AnchoredVolumeProfile)
        {
            _drawing.VolumeBucketCount = BucketCountCombo.SelectedItem is int bc ? bc : 24;
            _drawing.VolumeProfileWidthPercent = ProfileWidthSlider.Value;
            _drawing.VolumeProfileColor = _profileColor;
        }
        if (_drawing.Kind is DrawingKind.PriceRange or DrawingKind.DateAndPriceRange)
            _drawing.PriceRangeMode = PriceModeCombo.SelectedItem as string ?? "both";
        if (_drawing.Kind is DrawingKind.DateRange or DrawingKind.DateAndPriceRange)
            _drawing.DateRangeUnit = TimeUnitCombo.SelectedItem as string ?? "days";

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

        if (FibSection.Visibility == Visibility.Visible && FibLevelsEditor.Levels != null)
        {
            _drawing.FibLevels = new List<FibLevel>(FibLevelsEditor.Levels);
        }

        if (_extendLeft is not null) _drawing.ExtendLeft = _extendLeft.IsChecked == true;
        if (_extendRight is not null) _drawing.ExtendRight = _extendRight.IsChecked == true;
        if (_showPriceLabels is not null) _drawing.ShowPriceLabels = _showPriceLabels.IsChecked == true;
        if (_showTimeLabel is not null) _drawing.ShowTimeLabel = _showTimeLabel.IsChecked == true;
        if (_showPriceChange is not null) _drawing.ShowPriceChange = _showPriceChange.IsChecked == true;
        if (_showBarCount is not null) _drawing.ShowBarCount = _showBarCount.IsChecked == true;
        if (_showTimeElapsed is not null) _drawing.ShowTimeElapsed = _showTimeElapsed.IsChecked == true;
        if (_showAngle is not null) _drawing.ShowAngle = _showAngle.IsChecked == true;

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