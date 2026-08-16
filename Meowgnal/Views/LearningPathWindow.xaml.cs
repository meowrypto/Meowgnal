using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Meowgnal.DataProviders;
using Meowgnal.Engine;
using Meowgnal.Models;
using Meowgnal.Services;

namespace Meowgnal.Views;

public partial class LearningPathWindow : Window
{
    private int _currentStep;
    private readonly AppSettings _settings;

    public LearningPathWindow()
    {
        InitializeComponent();
        _settings = SettingsStorageService.Load();
        _currentStep = Math.Clamp(_settings.LearningPathStepCompleted + 1, 1, 4);
        RenderStep();
    }

    #region Title bar

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    #endregion

    private void RenderStep()
    {
        var step = LearningPathSteps.All[_currentStep - 1];
        StepTitle.Text = step.Title;
        StepDescription.Text = step.Description;
        ProgressText.Text = $"Step {_currentStep} of 4";
        ProgressBar.Value = _currentStep;

        GoButton.Content = _currentStep switch
        {
            1 => "📦 Open Template Store",
            2 => "🧪 Run quick backtest",
            3 => "✏️ Open Strategy Builder",
            4 => "🚀 Open empty Strategy Builder",
            _ => "Go"
        };
    }

    private async void GoButton_Click(object sender, RoutedEventArgs e)
    {
        switch (_currentStep)
        {
            case 1: OpenTemplateStore(); break;
            case 2: await RunQuickBacktest(); break;
            case 3: OpenBuilderWithTweak(); break;
            case 4: OpenEmptyBuilder(); break;
        }
    }

    private void OpenTemplateStore()
    {
        var store = new TemplateStoreWindow("BTC/USDT") { Owner = this };
        store.ShowDialog();
        CompleteStep();
    }

    private async System.Threading.Tasks.Task RunQuickBacktest()
    {
        var strategies = StrategyStorageService.LoadAll();
        if (strategies.Count == 0)
        {
            NotificationService.ShowToast("Meowgnal", "No strategy found. Complete step 1 first.");
            return;
        }

        var strategy = strategies[^1];
        IDataProvider provider = strategy.DataSource == "hyperliquid"
            ? new HyperliquidDataProvider()
            : new BinanceDataProvider();

        var bars = await provider.GetHistoricalCandlesAsync(strategy.Symbol, strategy.Timeframe, limit: 500);
        if (bars.Count < 50)
        {
            NotificationService.ShowToast("Meowgnal", "Not enough data for a quick backtest.");
            return;
        }

        await IndicatorEngine.PrefetchFundamentalsAsync(bars, strategy.Indicators, strategy.DataSource, strategy.Symbol);
        var result = BacktestEngine.Run(strategy, bars, 10000m, 0.1m, 0.05m);

        var win = new BacktestWindow(strategy, result) { Owner = this };
        win.ShowDialog();
        CompleteStep();
    }

    private void OpenBuilderWithTweak()
    {
        var strategies = StrategyStorageService.LoadAll();
        if (strategies.Count == 0)
        {
            NotificationService.ShowToast("Meowgnal", "No strategy found. Complete step 1 first.");
            return;
        }

        var strategy = strategies[^1];
        var builder = new StrategyBuilderWindow(strategy) { Owner = this };
        builder.ShowDialog();
        CompleteStep();
    }

    private void OpenEmptyBuilder()
    {
        var builder = new StrategyBuilderWindow { Owner = this };
        builder.ShowDialog();
        CompleteStep();
    }

    private void CompleteStep()
    {
        _settings.LearningPathStepCompleted = _currentStep;
        SettingsStorageService.Save(_settings);

        if (_currentStep >= 4)
        {
            NotificationService.ShowToast("Meowgnal", "🎉 Learning path complete! You're ready to trade.");
            Close();
            return;
        }

        _currentStep++;
        RenderStep();
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        _settings.LearningPathStepCompleted = 4;
        SettingsStorageService.Save(_settings);
        Close();
    }
}