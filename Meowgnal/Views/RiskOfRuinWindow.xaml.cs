using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Meowgnal.Models;
using Meowgnal.Services;

namespace Meowgnal.Views;

public partial class RiskOfRuinWindow : Window
{
    public RiskOfRuinWindow() : this(null, null) { }

    // Constructor that accepts defaults from a backtest result + strategy.
    public RiskOfRuinWindow(BacktestResult? result, StrategyDefinition? strategy)
    {
        InitializeComponent();

        // Pre-fill from backtest if available; otherwise use sensible defaults.
        var winRate = result?.WinRatePercent ?? 50;
        var riskPct = strategy?.RiskManagement?.PositionSizing?.RiskPercentPerTrade ?? 2;

        WinRateSlider.Value = Math.Clamp(winRate, 1, 99);
        RiskSlider.Value = Math.Clamp(riskPct, 0.5, 25);

        Recalculate();
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

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    #endregion

    private void Input_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Guard against calls before InitializeComponent finishes
        if (WinRateValueText is null) return;
        Recalculate();
    }

    private void Recalculate()
    {
        var winRate = WinRateSlider.Value / 100.0;
        var risk = RiskSlider.Value / 100.0;

        WinRateValueText.Text = $"{WinRateSlider.Value:F0}%";
        RiskValueText.Text = $"{RiskSlider.Value:F1}%";

        var ror = RiskOfRuinCalculator.Calculate(winRate, risk);
        var rorPercent = ror * 100;

        RuinPercentText.Text = $"{rorPercent:F2}%";

        // Color-coded severity
        if (rorPercent >= 20)
        {
            RuinPercentText.Foreground = (Brush)FindResource("Down");
            RuinLabel.Text = "🔴 High risk of account destruction";
            RuinLabel.Foreground = (Brush)FindResource("Down");
        }
        else if (rorPercent >= 5)
        {
            RuinPercentText.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xB9, 0x42));
            RuinLabel.Text = "⚡ Moderate risk";
            RuinLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xB9, 0x42));
        }
        else
        {
            RuinPercentText.Foreground = (Brush)FindResource("Up");
            RuinLabel.Text = "✅ Low risk";
            RuinLabel.Foreground = (Brush)FindResource("Up");
        }

        ExplanationText.Text =
            $"With a {WinRateSlider.Value:F0}% win rate and {RiskSlider.Value:F1}% risk per trade, " +
            $"there is a {rorPercent:F2}% chance of losing your entire account before making significant profit.";
    }
}