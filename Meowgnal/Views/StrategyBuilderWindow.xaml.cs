using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Meowgnal.DataProviders;
using Meowgnal.Engine;
using Meowgnal.Models;
using Meowgnal.Services;

namespace Meowgnal.Views;

public sealed class TokenOption
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
}

public sealed class OpOption
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
}

public sealed class ModeOption
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
}

// Represents either a leaf condition or a group of conditions in the UI tree.

public partial class StrategyBuilderWindow : Window
{
    public const string NumberToken = "__num__";

    public enum BuilderOpenMode { Customize, Edit, Copy }

    private readonly ObservableCollection<IndicatorRow> _indicators = new();
    private readonly ObservableCollection<ConditionNodeViewModel> _entryGroups = new();
    private readonly ObservableCollection<ConditionNodeViewModel> _exitGroups = new();

    private StrategyDefinition? _editing;
    private bool _isCopy;
    private readonly DispatcherTimer _descTimer;
    private BacktestResult? _lastTestResult;

    public ObservableCollection<IndicatorInfo> RegistryOptions { get; } = new();
    public ICollectionView GroupedRegistry { get; private set; } = null!;
    public ObservableCollection<OpOption> OpOptions { get; } = new();
    public ObservableCollection<ModeOption> ModeOptions { get; } = new();
    public ObservableCollection<TokenOption> TokenOptions { get; } = new();
    public ObservableCollection<TokenOption> TokenOptionsWithNumber { get; } = new();
    public ObservableCollection<string> StopMethods { get; } = new() { "Fixed percent", "ATR multiple" };
    public ObservableCollection<string> TargetMethods { get; } = new() { "Risk to reward ratio", "Fixed percent" };

    public StrategyBuilderWindow()
    {
        InitializeComponent();
        DataContext = this;

        foreach (var info in IndicatorRegistry.All) RegistryOptions.Add(info);
        GroupedRegistry = CollectionViewSource.GetDefaultView(RegistryOptions);
        GroupedRegistry.GroupDescriptions.Add(new PropertyGroupDescription("SubCategory"));

        OpOptions.Add(new OpOption { Key = "crossesAbove", Label = "crosses above" });
        OpOptions.Add(new OpOption { Key = "crossesBelow", Label = "crosses below" });
        OpOptions.Add(new OpOption { Key = "greaterThan", Label = "is greater than" });
        OpOptions.Add(new OpOption { Key = "lessThan", Label = "is less than" });
        OpOptions.Add(new OpOption { Key = "above", Label = "stays above" });
        OpOptions.Add(new OpOption { Key = "below", Label = "stays below" });
        OpOptions.Add(new OpOption { Key = "nearSupport", Label = "is near support" });
        OpOptions.Add(new OpOption { Key = "nearResistance", Label = "is near resistance" });

        ModeOptions.Add(new ModeOption { Key = "all", Label = "ALL conditions must be true (AND)" });
        ModeOptions.Add(new ModeOption { Key = "any", Label = "ANY condition can be true (OR)" });
        ModeOptions.Add(new ModeOption { Key = "threshold", Label = "SCORE at least N (confluence)" });

        StopMethodCombo.ItemsSource = StopMethods;
        TargetMethodCombo.ItemsSource = TargetMethods;

        IndicatorsList.ItemsSource = _indicators;
        EntryList.ItemsSource = _entryGroups;
        ExitList.ItemsSource = _exitGroups;

        _indicators.Add(new IndicatorRow { Id = "ema9", Type = "EMA", Period = 9 });
        _indicators.Add(new IndicatorRow { Id = "ema21", Type = "EMA", Period = 21 });

        var entryRoot = new ConditionNodeViewModel(true) { Mode = "all", Depth = 0 };
        entryRoot.Children.Add(new ConditionNodeViewModel(false) { Left = "ema9", Op = "crossesAbove", Right = "ema21", Depth = 1, Parent = entryRoot });
        _entryGroups.Add(entryRoot);

        var exitRoot = new ConditionNodeViewModel(true) { Mode = "any", Depth = 0 };
        exitRoot.Children.Add(new ConditionNodeViewModel(false) { Left = "ema9", Op = "crossesBelow", Right = "ema21", Depth = 1, Parent = exitRoot });
        _exitGroups.Add(exitRoot);

        StopMethodCombo.SelectedItem = "Fixed percent";
        TargetMethodCombo.SelectedItem = "Risk to reward ratio";

        RefreshTokens();

        _descTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _descTimer.Tick += (_, _) => UpdateDescription();
        _descTimer.Start();
        UpdateDescription();
    }

    public StrategyBuilderWindow(StrategyDefinition prefill) : this()
    {
        if (prefill is null) return;

        Title = $"Customizing: {prefill.Name.Replace(" (Custom)", "")}";
        TitleText.Text = Title;
        NameBox.Text = prefill.Name;
        SymbolBox.Text = prefill.Symbol;
        TimeframeBox.Text = prefill.Timeframe;

        var source = prefill.DataSource == "hyperliquid" ? "hyperliquid" : "binance";
        foreach (var item in SourceCombo.Items)
        {
            if (item is ComboBoxItem ci && ci.Content?.ToString() == source)
                SourceCombo.SelectedItem = ci;
        }

        _indicators.Clear();
        foreach (var ind in prefill.Indicators)
        {
            var fallback = ind.Type == "MACD" ? 12 : 14;
            var period = ind.Params.TryGetValue("period", out var p) ? ToInt(p, fallback) : fallback;
            _indicators.Add(new IndicatorRow { Id = ind.Id, Type = ind.Type, Period = period });
        }

        _entryGroups.Clear();
        if (prefill.EntryRules is not null)
            _entryGroups.Add(LoadRootGroup(prefill.EntryRules, 0));

        _exitGroups.Clear();
        if (prefill.ExitRules is not null)
            _exitGroups.Add(LoadRootGroup(prefill.ExitRules, 0));

        EntryMinScoreBox.Text = prefill.EntryRules?.MinScore?.ToString() ?? "3";

        StopMethodCombo.SelectedItem = prefill.RiskManagement?.StopLoss?.Method == "ATR" ? "ATR multiple" : "Fixed percent";
        StopValueBox.Text = prefill.RiskManagement?.StopLoss?.Multiplier.ToString() ?? "2";
        TargetMethodCombo.SelectedItem = prefill.RiskManagement?.Target?.Method == "fixedPercent" ? "Fixed percent" : "Risk to reward ratio";
        TargetValueBox.Text = prefill.RiskManagement?.Target?.Value.ToString() ?? "2";
        RiskPercentBox.Text = prefill.RiskManagement?.PositionSizing?.RiskPercentPerTrade.ToString() ?? "1";

        RefreshTokens();
        UpdateDescription();
    }

    public StrategyBuilderWindow(StrategyDefinition prefill, BuilderOpenMode mode) : this(prefill)
    {
        if (mode == BuilderOpenMode.Edit)
        {
            _editing = prefill;
            Title = "Editing: " + prefill.Name;
            TitleText.Text = Title;
        }
        else if (mode == BuilderOpenMode.Copy)
        {
            _isCopy = true;
            Title = "Copying: " + prefill.Name;
            TitleText.Text = Title;
        }
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

    // Recursively loads a RuleGroup into ConditionNodeViewModel tree.
    // Loads the root RuleGroup (EntryRules or ExitRules) into a view model.
    private static ConditionNodeViewModel LoadRootGroup(RuleGroup group, int depth)
    {
        var vm = new ConditionNodeViewModel(true)
        {
            Mode = group.Mode,
            MinScore = group.MinScore,
            Depth = depth
        };

        LoadChildrenInto(vm, group.Conditions, depth);
        return vm;
    }

    // Loads a nested ConditionGroup into a view model.
    private static ConditionNodeViewModel LoadConditionGroup(ConditionGroup group, ConditionNodeViewModel parent, int depth)
    {
        var vm = new ConditionNodeViewModel(true)
        {
            Mode = group.Mode,
            MinScore = group.MinScore,
            Depth = depth,
            Parent = parent
        };

        LoadChildrenInto(vm, group.Conditions, depth);
        return vm;
    }

    // Helper: loads child nodes (leaf or group) into a parent view model.
    private static void LoadChildrenInto(ConditionNodeViewModel parent, List<ConditionNode>? nodes, int depth)
    {
        if (nodes is null) return;

        foreach (var node in nodes)
        {
            if (node is LeafCondition leaf)
            {
                parent.Children.Add(new ConditionNodeViewModel(false)
                {
                    Left = leaf.Left,
                    Op = leaf.Op,
                    Right = leaf.Right is double d ? NumberToken : (leaf.Right?.ToString() ?? "price"),
                    Number = leaf.Right is double dn ? dn.ToString() : "30",
                    Weight = leaf.Weight,
                    Tolerance = leaf.TolerancePercent,
                    Depth = depth + 1,
                    Parent = parent
                });
            }
            else if (node is ConditionGroup childGroup)
            {
                parent.Children.Add(LoadConditionGroup(childGroup, parent, depth + 1));
            }
        }
    }
    private static int ToInt(object? value, int fallback) => value switch
    {
        int i => i,
        long l => (int)l,
        double d => (int)d,
        string s when int.TryParse(s, out var n) => n,
        System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number => je.GetInt32(),
        _ => fallback
    };

    private StrategyDefinition BuildStrategyFromUi()
    {
        return new StrategyDefinition
        {
            StrategyId = _editing?.StrategyId ?? "preview",
            Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "My strategy" : NameBox.Text.Trim(),
            Symbol = SymbolBox.Text,
            Timeframe = TimeframeBox.Text,
            DataSource = SourceCombo.SelectedItem is ComboBoxItem ci ? ci.Content?.ToString() ?? "binance" : "binance",
            Indicators = _indicators.Select(i => new IndicatorDefinition
            {
                Id = i.Id,
                Type = i.Type,
                Params = i.Type == "MACD"
                    ? new() { ["fastPeriod"] = 12, ["slowPeriod"] = 26, ["signalPeriod"] = 9 }
                    : new() { ["period"] = i.Period }
            }).ToList(),
            EntryRules = BuildRootGroup(_entryGroups.FirstOrDefault()),
            ExitRules = BuildRootGroup(_exitGroups.FirstOrDefault()),
            RiskManagement = new RiskManagementConfig
            {
                StopLoss = new StopLossConfig
                {
                    Method = StopMethodCombo.SelectedItem as string == "ATR multiple" ? "ATR" : "fixedPercent",
                    Multiplier = double.TryParse(StopValueBox.Text, out var sv) ? sv : 2
                },
                Target = new TargetConfig
                {
                    Method = TargetMethodCombo.SelectedItem as string == "Fixed percent" ? "fixedPercent" : "riskRewardRatio",
                    Value = double.TryParse(TargetValueBox.Text, out var tv) ? tv : 2
                },
                PositionSizing = new PositionSizingConfig
                {
                    RiskPercentPerTrade = double.TryParse(RiskPercentBox.Text, out var rv) ? rv : 1
                }
            }
        };
    }

    // Recursively builds a RuleGroup from ConditionNodeViewModel tree.
    // Builds the root RuleGroup (EntryRules or ExitRules) from the view model.
    private static RuleGroup BuildRootGroup(ConditionNodeViewModel? vm)
    {
        if (vm is null)
        {
            return new RuleGroup
            {
                Mode = "all",
                MinScore = null,
                TriggerMode = "onTransition",
                Conditions = new List<ConditionNode>()
            };
        }

        return new RuleGroup
        {
            Mode = vm.Mode,
            MinScore = vm.MinScore,
            TriggerMode = "onTransition",
            Conditions = BuildConditions(vm)
        };
    }

    // Builds a nested ConditionGroup from the view model.
    private static ConditionGroup BuildConditionGroup(ConditionNodeViewModel vm)
    {
        return new ConditionGroup
        {
            Mode = vm.Mode,
            MinScore = vm.MinScore,
            Conditions = BuildConditions(vm)
        };
    }

    // Helper: builds the list of ConditionNode (leaf or group) from a view model.
    private static List<ConditionNode> BuildConditions(ConditionNodeViewModel vm)
    {
        var conditions = new List<ConditionNode>();

        foreach (var child in vm.Children)
        {
            if (child.IsLeaf)
            {
                if (!string.IsNullOrEmpty(child.Left) && !string.IsNullOrEmpty(child.Op))
                {
                    conditions.Add(new LeafCondition
                    {
                        Left = child.Left,
                        Op = child.Op,
                        Right = child.Right == NumberToken
                            ? (double.TryParse(child.Number, out var n) ? n : 0d)
                            : child.Right,
                        Weight = child.Weight,
                        TolerancePercent = child.Tolerance
                    });
                }
            }
            else if (child.IsGroup)
            {
                var childGroup = BuildConditionGroup(child);
                if (childGroup.Conditions.Count > 0)
                    conditions.Add(childGroup);
            }
        }

        return conditions;
    }
    private void UpdateDescription()
    {
        if (DescriptionText is null) return;
        try
        {
            DescriptionText.Text = StrategyDescriptionService.Describe(BuildStrategyFromUi());
        }
        catch
        {
            DescriptionText.Text = "—";
        }
    }

    private void RefreshTokens()
    {
        TokenOptions.Clear();
        TokenOptions.Add(new TokenOption { Id = "price", Label = "price (last close)" });
        TokenOptions.Add(new TokenOption { Id = "volume", Label = "volume" });

        foreach (var ind in _indicators)
        {
            var info = IndicatorRegistry.All.FirstOrDefault(i => i.Type == ind.Type);

            if (ind.Type == "MACD")
            {
                TokenOptions.Add(new TokenOption { Id = ind.Id, Label = $"MACD line ({ind.Id})" });
                TokenOptions.Add(new TokenOption { Id = "signal", Label = $"MACD signal ({ind.Id})" });
            }
            else if (info?.SubOutputs is not null)
            {
                foreach (var sub in info.SubOutputs)
                    TokenOptions.Add(new TokenOption { Id = $"{ind.Id}.{sub}", Label = $"{ind.Type} {sub} ({ind.Id}.{sub})" });
            }
            else
            {
                TokenOptions.Add(new TokenOption { Id = ind.Id, Label = $"{ind.Type} {ind.Period} ({ind.Id})" });
            }
        }

        TokenOptionsWithNumber.Clear();
        foreach (var t in TokenOptions)
            TokenOptionsWithNumber.Add(new TokenOption { Id = t.Id, Label = t.Label });
        TokenOptionsWithNumber.Add(new TokenOption { Id = NumberToken, Label = "a number…" });
    }

    private static string MakeId(string type, int period, ObservableCollection<IndicatorRow> rows)
    {
        var baseId = type == "MACD" ? "macd" : type.ToLowerInvariant() + period;
        var id = baseId;
        var n = 2;
        while (rows.Any(r => r.Id == id))
        {
            id = baseId + "_" + n;
            n++;
        }
        return id;
    }

    private void AddIndicator_Click(object sender, RoutedEventArgs e)
    {
        var info = IndicatorRegistry.All[0];
        var id = MakeId(info.Type, info.DefaultPeriod, _indicators);
        _indicators.Add(new IndicatorRow { Id = id, Type = info.Type, Period = info.DefaultPeriod });
        RefreshTokens();
        UpdateDescription();
    }

    private void RemoveIndicator_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not IndicatorRow row) return;
        _indicators.Remove(row);
        RefreshTokens();
        UpdateDescription();
    }

    private void IndicatorType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (e.RemovedItems.Count == 0) return; // Ignore initial selection during window load
        if (sender is not ComboBox combo || combo.DataContext is not IndicatorRow row) return;
        var info = IndicatorRegistry.All.FirstOrDefault(i => i.Type == row.Type);
        if (info is null) return;
        row.Period = info.DefaultPeriod;
        row.Id = MakeId(row.Type, row.Period, _indicators);
        RefreshTokens();
        UpdateDescription();
    }

    // Adds a new leaf condition to the specified parent group.
    private void AddCondition_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ConditionNodeViewModel parent) return;
        parent.Children.Add(new ConditionNodeViewModel(false) { Depth = parent.Depth + 1, Parent = parent });
        UpdateDescription();
    }

    // Adds a new nested group to the specified parent group (max depth = 3).
    private void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ConditionNodeViewModel parent) return;
        if (parent.Depth >= 3) return;

        var newGroup = new ConditionNodeViewModel(true)
        {
            Mode = "all",
            Depth = parent.Depth + 1,
            Parent = parent
        };
        newGroup.Children.Add(new ConditionNodeViewModel(false) { Depth = newGroup.Depth + 1, Parent = newGroup });
        parent.Children.Add(newGroup);
        UpdateDescription();
    }

    // Removes a condition or group from its parent.
    private void RemoveNode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ConditionNodeViewModel node) return;
        if (node.Parent is null) return; // Root cannot be removed

        node.Parent.Children.Remove(node);
        UpdateDescription();
    }

    #region Quick test

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        TestButton.IsEnabled = false;
        TestButton.Content = "⏳ Testing...";
        TestResultPanel.Visibility = Visibility.Visible;
        TestResultStatus.Text = "Fetching data and running backtest...";
        TestWinRate.Text = "—";
        TestTrades.Text = "—";
        TestRR.Text = "—";
        TestDD.Text = "—";
        SeeFullReportButton.Visibility = Visibility.Collapsed;

        try
        {
            var strategy = BuildStrategyFromUi();
            strategy.StrategyId = Guid.NewGuid().ToString("N");

            if (string.IsNullOrWhiteSpace(strategy.Symbol))
            {
                TestResultStatus.Text = "❌ Error: Symbol is required.";
                return;
            }

            if (strategy.EntryRules.Conditions.Count == 0)
            {
                TestResultStatus.Text = "❌ Error: At least one entry condition is required.";
                return;
            }

            var days = TestPeriodCombo.SelectedItem is ComboBoxItem ci && ci.Tag is string tag && int.TryParse(tag, out var d) ? d : 90;
            var limit = days * 24;

            IDataProvider provider = strategy.DataSource == "hyperliquid"
                ? new HyperliquidDataProvider()
                : new BinanceDataProvider();

            var bars = await provider.GetHistoricalCandlesAsync(strategy.Symbol, strategy.Timeframe, limit);
            if (bars.Count < 50)
            {
                TestResultStatus.Text = "❌ Error: Not enough historical data to run backtest.";
                return;
            }

            await IndicatorEngine.PrefetchFundamentalsAsync(bars, strategy.Indicators, strategy.DataSource, strategy.Symbol);
            var result = BacktestEngine.Run(strategy, bars, 10000m, 0.1m, 0.05m);
            _lastTestResult = result;

            TestResultStatus.Text = $"✅ Backtest complete ({bars.Count} candles, {days} days)";
            TestWinRate.Text = $"{result.WinRatePercent:N1}%";
            TestWinRate.Foreground = result.WinRatePercent >= 50 ? (Brush)FindResource("Up") : (Brush)FindResource("Down");
            TestTrades.Text = result.Trades.Count.ToString();
            TestRR.Text = result.AverageRiskReward.ToString("N2");
            TestDD.Text = $"{result.MaxDrawdownPercent:N1}%";
            SeeFullReportButton.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            TestResultStatus.Text = $"❌ Error: {ex.Message}";
        }
        finally
        {
            TestButton.IsEnabled = true;
            TestButton.Content = "🧪 Test this strategy";
        }
    }

    private void SeeFullReport_Click(object sender, RoutedEventArgs e)
    {
        if (_lastTestResult is null) return;
        var strategy = BuildStrategyFromUi();
        strategy.StrategyId = Guid.NewGuid().ToString("N");

        var win = new BacktestWindow(strategy, _lastTestResult) { Owner = this };
        win.ShowDialog();
    }

    #endregion

    private void SaveStrategy_Click(object sender, RoutedEventArgs e)
    {
        var strategy = BuildStrategyFromUi();

        if (_editing is not null) strategy.StrategyId = _editing.StrategyId;
        else strategy.StrategyId = Guid.NewGuid().ToString("N");

        if (_isCopy && !strategy.Name.EndsWith("(Copy)")) strategy.Name += " (Copy)";

        var existing = StrategyStorageService.LoadAll();
        var baseName = strategy.Name;
        var counter = 1;
        while (existing.Any(s => s.StrategyId != strategy.StrategyId && string.Equals(s.Name, strategy.Name, StringComparison.OrdinalIgnoreCase)))
        {
            counter++;
            strategy.Name = $"{baseName} ({counter})";
        }

        StrategyStorageService.Save(strategy);
        StatusText.Text = _editing is not null ? $"Updated '{strategy.Name}'" : $"Saved as '{strategy.Name}'";
    }
}