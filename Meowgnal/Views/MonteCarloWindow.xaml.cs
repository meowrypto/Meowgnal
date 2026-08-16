using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Meowgnal.Engine;
using Meowgnal.Models;

namespace Meowgnal.Views;

public partial class MonteCarloWindow : Window
{
    private readonly BacktestResult _result;
    private readonly StrategyDefinition? _strategy;

    public MonteCarloWindow(BacktestResult result, StrategyDefinition? strategy)
    {
        InitializeComponent();
        _result = result;
        _strategy = strategy;

        TitleText.Text = _strategy is null
            ? "🎲 Monte Carlo Analysis"
            : $"🎲 Monte Carlo Analysis — {_strategy.Name}";
        TradesPerSimBox.Text = result.Trades.Count.ToString();

        Loaded += async (_, _) => await RunSimulationAsync();
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

    private async void RunSim_Click(object sender, RoutedEventArgs e) => await RunSimulationAsync();

    // Runs the simulation on a background thread so the UI never freezes.
    private async Task RunSimulationAsync()
    {
        if (_result.Trades.Count == 0)
        {
            SummaryText.Text = "No trades in this backtest — nothing to simulate.";
            return;
        }

        RunButton.IsEnabled = false;
        RunButton.Content = "⏳ Simulating...";

        try
        {
            var simCount = int.TryParse(SimCountBox.Text, out var sc) && sc >= 10 ? System.Math.Min(sc, 20000) : 1000;
            var tradesPerSim = int.TryParse(TradesPerSimBox.Text, out var tp) && tp >= 1 ? System.Math.Min(tp, 5000) : _result.Trades.Count;

            var input = new MonteCarloInput
            {
                TradeReturns = _result.Trades.Select(t => (decimal)t.PnLPercent).ToList(),
                StartingBalance = _result.StartingBalance,
                SimulationCount = simCount,
                TradesPerSimulation = tradesPerSim,
                WithReplacement = true,
                RandomSeed = null,
                BlockSize = 1
            };

            var mc = await MonteCarloEngine.RunAsync(input);

            RenderChart(mc);
            RenderCards(mc);
            SummaryText.Text = BuildSummary(mc);
        }
        catch (Exception ex)
        {
            SummaryText.Text = $"Simulation failed: {ex.Message}";
        }
        finally
        {
            RunButton.IsEnabled = true;
            RunButton.Content = "▶ Run Simulation";
        }
    }

    private void RenderCards(MonteCarloResult mc)
    {
        MedianFinalText.Text = $"{mc.MedianFinalBalance:N0}";
        BestFinalText.Text = $"{mc.P95FinalBalance:N0}";
        WorstDDText.Text = $"{mc.WorstCaseMaxDrawdown:N1}%";
        RuinText.Text = $"{mc.RuinProbability:N1}%";

        RuinText.Foreground = mc.RuinProbability >= 20
            ? (Brush)FindResource("Down")
            : mc.RuinProbability >= 5
                ? new SolidColorBrush(Color.FromRgb(0xF5, 0xB9, 0x42))
                : (Brush)FindResource("Up");
    }

    private static string BuildSummary(MonteCarloResult mc) =>
        $"In {mc.TotalSimulations:N0} simulated futures, your median outcome is {mc.MedianFinalBalance:N0} USDT, " +
        $"but the worst 5% of scenarios saw drawdowns of {mc.WorstCaseMaxDrawdown:N1}% or more. " +
        $"There is a {mc.RuinProbability:N1}% chance of losing most of your account.";

    // Draws the cone of uncertainty: P95 (green), P5 (red) and the median (blue),
    // with a shaded area between P5 and P95.
    private void RenderChart(MonteCarloResult mc)
    {
        var p5 = mc.EquityCurveP5.Select((v, i) => new ObservablePoint(i, (double)v)).ToList();
        var p50 = mc.EquityCurveP50.Select((v, i) => new ObservablePoint(i, (double)v)).ToList();
        var p95 = mc.EquityCurveP95.Select((v, i) => new ObservablePoint(i, (double)v)).ToList();

        var up = ToSkColor("Up");
        var down = ToSkColor("Down");
        var accent = ToSkColor("Accent");
        var textMuted = ToSkColor("TextMuted");
        var grid = ToSkColor("BorderColor");
        var panel = ToSkColor("PanelBg");

        McChart.Series = new ISeries[]
        {
            // P95 with a light green fill down to the axis
            new LineSeries<ObservablePoint>
            {
                Values = new ObservableCollection<ObservablePoint>(p95),
                Stroke = new SolidColorPaint(new SKColor(up.Red, up.Green, up.Blue, 140)) { StrokeThickness = 1 },
                Fill = new SolidColorPaint(new SKColor(up.Red, up.Green, up.Blue, 40)),
                GeometrySize = 0,
                Name = "P95 (best)"
            },
            // P5 with a solid panel-colored fill that masks everything below it,
            // leaving a visible shaded band only between P5 and P95.
            new LineSeries<ObservablePoint>
            {
                Values = new ObservableCollection<ObservablePoint>(p5),
                Stroke = new SolidColorPaint(new SKColor(down.Red, down.Green, down.Blue, 140)) { StrokeThickness = 1 },
                Fill = new SolidColorPaint(panel),
                GeometrySize = 0,
                Name = "P5 (worst)"
            },
            // Median on top
            new LineSeries<ObservablePoint>
            {
                Values = new ObservableCollection<ObservablePoint>(p50),
                Stroke = new SolidColorPaint(accent) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 0,
                Name = "Median"
            }
        };

        McChart.XAxes = new[]
        {
            new Axis
            {
                Labeler = v => $"#{v:0}",
                LabelsPaint = new SolidColorPaint(textMuted),
                SeparatorsPaint = new SolidColorPaint(grid),
                TextSize = 11
            }
        };
        McChart.YAxes = new[]
        {
            new Axis
            {
                Labeler = v => v.ToString("N0"),
                LabelsPaint = new SolidColorPaint(textMuted),
                SeparatorsPaint = new SolidColorPaint(grid),
                TextSize = 11
            }
        };
    }

    // Converts a WPF brush resource key to an SKColor for LiveCharts.
    private SKColor ToSkColor(string brushKey)
    {
        if (FindResource(brushKey) is SolidColorBrush brush)
        {
            var c = brush.Color;
            return new SKColor(c.R, c.G, c.B, c.A);
        }
        return SKColors.Gray;
    }
}