using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Meowgnal.Engine;
using Meowgnal.Models;
using Meowgnal.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Meowgnal.Models;
using Meowgnal.Services;

namespace Meowgnal.Views;

public partial class RiskOfRuinWindow : Window
{
    private readonly BacktestResult? _backtestResult;

    public RiskOfRuinWindow() : this(null, null) { }

    // Constructor that accepts defaults from a backtest result + strategy.
    public RiskOfRuinWindow(BacktestResult? result, StrategyDefinition? strategy)
    {
        InitializeComponent();

        _backtestResult = result;

        // Monte Carlo is only available when a real backtest with enough trades exists.
        var mcAvailable = result is not null && result.Trades.Count >= 10;
        MonteCarloCheck.IsEnabled = mcAvailable;
        if (!mcAvailable)
            MonteCarloHint.Text = result is null
                ? "Monte Carlo needs a backtest result — open this window from a backtest report."
                : "Monte Carlo needs at least 10 backtest trades.";

        // Pre-fill from backtest if available; otherwise use sensible defaults.
        {
            InitializeComponent();

            // Pre-fill from backtest if available; otherwise use sensible defaults.
            var winRate = result?.WinRatePercent ?? 50;
            var riskPct = strategy?.RiskManagement?.PositionSizing?.RiskPercentPerTrade ?? 2;

            WinRateSlider.Value = Math.Clamp(winRate, 1, 99);
            RiskSlider.Value = Math.Clamp(riskPct, 0.5, 25);

            Recalculate();
        }
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
        // Guard: while the XAML is still loading, some controls don't exist yet.
        if (WinRateSlider is null || RiskSlider is null ||
            WinRateValueText is null || RiskValueText is null ||
            RuinPercentText is null || RuinLabel is null || ExplanationText is null ||
            MonteCarloCheck is null)
        {
            return;
        }

        Recalculate();
    }


    private void MonteCarloCheck_Changed(object sender, RoutedEventArgs e) => Recalculate();

    private void Recalculate()
    {
        var useMc = MonteCarloCheck.IsEnabled && MonteCarloCheck.IsChecked == true;

        WinRateValueText.Text = $"{WinRateSlider.Value:F0}%";
        RiskValueText.Text = $"{RiskSlider.Value:F1}%";

        // While Monte Carlo is on, the hypothetical sliders are not needed.
        WinRateSlider.IsEnabled = !useMc;
        RiskSlider.IsEnabled = !useMc;

        double rorPercent;
        if (useMc && _backtestResult is not null && _backtestResult.Trades.Count >= 10)
        {
            var input = new MonteCarloInput
            {
                TradeReturns = _backtestResult.Trades.Select(t => (decimal)t.PnLPercent).ToList(),
                StartingBalance = _backtestResult.StartingBalance,
                SimulationCount = 1000,
                TradesPerSimulation = _backtestResult.Trades.Count
            };
            var mc = MonteCarloEngine.Run(input);
            rorPercent = mc.RuinProbability;
            ExplanationText.Text =
                $"Based on a Monte Carlo simulation of {_backtestResult.Trades.Count} real backtest trades " +
                $"(1,000 simulated futures), there is a {rorPercent:F2}% chance of losing most of your account.";
        }
        else
        {
            var winRate = WinRateSlider.Value / 100.0;
            var risk = RiskSlider.Value / 100.0;
            rorPercent = RiskOfRuinCalculator.Calculate(winRate, risk) * 100;
            ExplanationText.Text =
                $"With a {WinRateSlider.Value:F0}% win rate and {RiskSlider.Value:F1}% risk per trade, " +
                $"there is a {rorPercent:F2}% chance of losing your entire account before making significant profit.";
        }

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