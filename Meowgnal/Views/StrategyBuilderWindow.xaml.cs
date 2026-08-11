using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Meowgnal.Models;
using Meowgnal.Services;

namespace Meowgnal.Views;

public partial class StrategyBuilderWindow : Window
{
    private readonly ObservableCollection<IndicatorRow> _indicators = new();
    private readonly ObservableCollection<ConditionRow> _entryConditions = new();
    private readonly ObservableCollection<ConditionRow> _exitConditions = new();

    public StrategyBuilderWindow()
    {
        InitializeComponent();
        IndicatorsList.ItemsSource = _indicators;
        EntryConditionsList.ItemsSource = _entryConditions;
        ExitConditionsList.ItemsSource = _exitConditions;

        // A sensible starting point so the form isn't empty.
        _indicators.Add(new IndicatorRow { Id = "ema9", Type = "EMA", Period = 9 });
        _indicators.Add(new IndicatorRow { Id = "ema21", Type = "EMA", Period = 21 });
        _entryConditions.Add(new ConditionRow { Left = "ema9", Op = "crossesAbove", Right = "ema21", Weight = 1 });
        _exitConditions.Add(new ConditionRow { Left = "ema9", Op = "crossesBelow", Right = "ema21" });
    }
    /// <summary>
    /// Opens the builder pre-filled from a template (used by the "Customize" flow).
    /// </summary>
    public StrategyBuilderWindow(StrategyDefinition prefill) : this()
    {
        if (prefill is null) return;

        Title = $"Customizing: {prefill.Name.Replace(" (Custom)", "")}";
        NameBox.Text = prefill.Name;
        SymbolBox.Text = prefill.Symbol;
        TimeframeBox.Text = prefill.Timeframe;

        // Replace the default indicators/conditions with those from the template
        _indicators.Clear();
        foreach (var ind in prefill.Indicators)
        {
            var period = ind.Params.TryGetValue("period", out var p) ? (int)p : 14;
            _indicators.Add(new IndicatorRow { Id = ind.Id, Type = ind.Type, Period = period });
        }

        _entryConditions.Clear();
        foreach (var cond in prefill.EntryRules.Conditions.OfType<LeafCondition>())
        {
            _entryConditions.Add(new ConditionRow
            {
                Left = cond.Left,
                Op = cond.Op,
                Right = cond.Right is double d ? d.ToString() : cond.Right?.ToString() ?? "",
                Weight = cond.Weight
            });
        }
        EntryModeCombo.SelectedItem = prefill.EntryRules.Mode;
        EntryMinScoreBox.Text = prefill.EntryRules.MinScore?.ToString() ?? "3";

        _exitConditions.Clear();
        foreach (var cond in prefill.ExitRules.Conditions.OfType<LeafCondition>())
        {
            _exitConditions.Add(new ConditionRow
            {
                Left = cond.Left,
                Op = cond.Op,
                Right = cond.Right is double d ? d.ToString() : cond.Right?.ToString() ?? "",
                Weight = cond.Weight
            });
        }
        ExitModeCombo.SelectedItem = prefill.ExitRules.Mode;
    }
    private void AddIndicator_Click(object sender, RoutedEventArgs e) =>
        _indicators.Add(new IndicatorRow { Id = $"ind{_indicators.Count + 1}" });

    private void AddEntryCondition_Click(object sender, RoutedEventArgs e) =>
        _entryConditions.Add(new ConditionRow());

    private void AddExitCondition_Click(object sender, RoutedEventArgs e) =>
        _exitConditions.Add(new ConditionRow());

    private void SaveStrategy_Click(object sender, RoutedEventArgs e)
    {
        var strategy = new StrategyDefinition
        {
            StrategyId = Guid.NewGuid().ToString("N"),
            Name = NameBox.Text,
            Symbol = SymbolBox.Text,
            Timeframe = TimeframeBox.Text,
            DataSource = "binance",
            Indicators = _indicators.Select(i => new IndicatorDefinition
            {
                Id = i.Id,
                Type = i.Type,
                Params = new() { ["period"] = i.Period }
            }).ToList(),
            EntryRules = new RuleGroup
            {
                Mode = (string)EntryModeCombo.SelectedItem,
                MinScore = double.TryParse(EntryMinScoreBox.Text, out var s) ? s : null,
                TriggerMode = "onTransition",
                Conditions = _entryConditions.Select(ToLeafCondition).Cast<ConditionNode>().ToList()
            },
            ExitRules = new RuleGroup
            {
                Mode = (string)ExitModeCombo.SelectedItem,
                TriggerMode = "onTransition",
                Conditions = _exitConditions.Select(ToLeafCondition).Cast<ConditionNode>().ToList()
            }
        };

        StrategyStorageService.Save(strategy);
        StatusText.Text = $"Saved as '{strategy.Name}' (encrypted, id: {strategy.StrategyId[..8]}...)";
    }

    private static LeafCondition ToLeafCondition(ConditionRow row) => new()
    {
        Left = row.Left,
        Op = row.Op,
        Right = double.TryParse(row.Right, out var num) ? num : row.Right,
        Weight = row.Weight
    };
}