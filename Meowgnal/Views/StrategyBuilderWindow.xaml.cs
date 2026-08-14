using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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

/// <summary>One sentence row: [left token] [operator] [right token or number].</summary>
public sealed class SentenceRow
{
    public string Left { get; set; } = "price";
    public string Op { get; set; } = "crossesAbove";
    public string Right { get; set; } = StrategyBuilderWindow.NumberToken;
    public string Number { get; set; } = "30";
    public double Weight { get; set; } = 1;
}

public partial class StrategyBuilderWindow : Window
{
    public const string NumberToken = "__num__";

    /// <summary>How the builder was opened from the Strategy Manager.</summary>
    public enum BuilderOpenMode { Customize, Edit, Copy }

    private readonly ObservableCollection<IndicatorRow> _indicators = new();
    private readonly ObservableCollection<SentenceRow> _entry = new();
    private readonly ObservableCollection<SentenceRow> _exit = new();

    // When set, Save keeps this strategy's StrategyId (edit mode).
    private StrategyDefinition? _editing;
    // When true, Save creates a new id and appends "(Copy)" to the name.
    private bool _isCopy;

    // Refreshes the live plain-English summary while the user edits.
    private readonly DispatcherTimer _descTimer;

    public ObservableCollection<IndicatorInfo> RegistryOptions { get; } = new();
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

        OpOptions.Add(new OpOption { Key = "crossesAbove", Label = "crosses above" });
        OpOptions.Add(new OpOption { Key = "crossesBelow", Label = "crosses below" });
        OpOptions.Add(new OpOption { Key = "greaterThan", Label = "is greater than" });
        OpOptions.Add(new OpOption { Key = "lessThan", Label = "is less than" });
        OpOptions.Add(new OpOption { Key = "above", Label = "stays above" });
        OpOptions.Add(new OpOption { Key = "below", Label = "stays below" });

        ModeOptions.Add(new ModeOption { Key = "all", Label = "ALL conditions must be true (AND)" });
        ModeOptions.Add(new ModeOption { Key = "any", Label = "ANY condition can be true (OR)" });
        ModeOptions.Add(new ModeOption { Key = "threshold", Label = "SCORE at least N (confluence)" });

        StopMethodCombo.ItemsSource = StopMethods;
        TargetMethodCombo.ItemsSource = TargetMethods;

        IndicatorsList.ItemsSource = _indicators;
        EntryList.ItemsSource = _entry;
        ExitList.ItemsSource = _exit;

        // A sensible starter so the form is never empty
        _indicators.Add(new IndicatorRow { Id = "ema9", Type = "EMA", Period = 9 });
        _indicators.Add(new IndicatorRow { Id = "ema21", Type = "EMA", Period = 21 });
        _entry.Add(new SentenceRow { Left = "ema9", Op = "crossesAbove", Right = "ema21" });
        _exit.Add(new SentenceRow { Left = "ema9", Op = "crossesBelow", Right = "ema21" });

        EntryModeCombo.SelectedValue = "all";
        ExitModeCombo.SelectedValue = "any";
        StopMethodCombo.SelectedItem = "Fixed percent";
        TargetMethodCombo.SelectedItem = "Risk to reward ratio";

        RefreshTokens();

        // Live summary: refresh on a short interval so every edit is reflected.
        _descTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _descTimer.Tick += (_, _) => UpdateDescription();
        _descTimer.Start();
        UpdateDescription();
    }

    /// <summary>Opens the builder pre-filled from a template or wizard result (Customize flow).</summary>
    public StrategyBuilderWindow(StrategyDefinition prefill) : this()
    {
        if (prefill is null) return;

        Title = $"Customizing: {prefill.Name.Replace(" (Custom)", "")}";
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

        _entry.Clear();
        foreach (var c in prefill.EntryRules.Conditions.OfType<LeafCondition>())
            _entry.Add(ToSentenceRow(c));

        _exit.Clear();
        foreach (var c in prefill.ExitRules.Conditions.OfType<LeafCondition>())
            _exit.Add(ToSentenceRow(c));

        EntryModeCombo.SelectedValue = prefill.EntryRules.Mode;
        ExitModeCombo.SelectedValue = prefill.ExitRules.Mode;
        EntryMinScoreBox.Text = prefill.EntryRules.MinScore?.ToString() ?? "3";

        StopMethodCombo.SelectedItem = prefill.RiskManagement.StopLoss.Method == "ATR" ? "ATR multiple" : "Fixed percent";
        StopValueBox.Text = prefill.RiskManagement.StopLoss.Multiplier.ToString();
        TargetMethodCombo.SelectedItem = prefill.RiskManagement.Target.Method == "fixedPercent" ? "Fixed percent" : "Risk to reward ratio";
        TargetValueBox.Text = prefill.RiskManagement.Target.Value.ToString();
        RiskPercentBox.Text = prefill.RiskManagement.PositionSizing.RiskPercentPerTrade.ToString();

        RefreshTokens();
        UpdateDescription();
    }

    /// <summary>Opens the builder from the Strategy Manager.</summary>
    /// <param name="prefill">Strategy whose values fill the form.</param>
    /// <param name="mode">Edit keeps the same StrategyId; Copy creates a new id with a "(Copy)" name.</param>
    public StrategyBuilderWindow(StrategyDefinition prefill, BuilderOpenMode mode) : this(prefill)
    {
        if (mode == BuilderOpenMode.Edit)
        {
            _editing = prefill;
            Title = "Editing: " + prefill.Name;
        }
        else if (mode == BuilderOpenMode.Copy)
        {
            _isCopy = true;
            Title = "Copying: " + prefill.Name;
        }
    }

    private static SentenceRow ToSentenceRow(LeafCondition c) => new()
    {
        Left = c.Left,
        Op = c.Op,
        Right = c.Right is double d ? NumberToken : (c.Right?.ToString() ?? "price"),
        Number = c.Right is double dn ? dn.ToString() : "30",
        Weight = c.Weight
    };

    // Safely converts a parameter value (int/long/double/string/JsonElement) to int.
    private static int ToInt(object? value, int fallback) => value switch
    {
        int i => i,
        long l => (int)l,
        double d => (int)d,
        string s when int.TryParse(s, out var n) => n,
        System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Number => je.GetInt32(),
        _ => fallback
    };

    // Builds a strategy object from the current UI state (used by Save and by the live summary).
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
            EntryRules = BuildGroup(EntryModeCombo.SelectedValue as string, EntryMinScoreBox.Text, _entry),
            ExitRules = BuildGroup(ExitModeCombo.SelectedValue as string, null, _exit),
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

    // Rebuilds the plain-English summary shown at the bottom of the window.
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

    // Rebuilds the dropdown tokens from the current indicator rows.
    private void RefreshTokens()
    {
        TokenOptions.Clear();
        TokenOptions.Add(new TokenOption { Id = "price", Label = "price (last close)" });
        TokenOptions.Add(new TokenOption { Id = "volume", Label = "volume" });

        foreach (var ind in _indicators)
        {
            if (ind.Type == "MACD")
            {
                TokenOptions.Add(new TokenOption { Id = ind.Id, Label = $"MACD line ({ind.Id})" });
                TokenOptions.Add(new TokenOption { Id = "signal", Label = $"MACD signal ({ind.Id})" });
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
        // When the user picks a new type from the registry, apply its default period and a fresh id.
        if (sender is not ComboBox combo || combo.DataContext is not IndicatorRow row) return;
        var info = IndicatorRegistry.All.FirstOrDefault(i => i.Type == row.Type);
        if (info is null) return;
        row.Period = info.DefaultPeriod;
        row.Id = MakeId(row.Type, row.Period, _indicators);
        RefreshTokens();
        UpdateDescription();
    }

    private void AddEntry_Click(object sender, RoutedEventArgs e)
    {
        _entry.Add(new SentenceRow());
        UpdateDescription();
    }

    private void RemoveEntry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not SentenceRow row) return;
        _entry.Remove(row);
        UpdateDescription();
    }

    private void AddExit_Click(object sender, RoutedEventArgs e)
    {
        _exit.Add(new SentenceRow());
        UpdateDescription();
    }

    private void RemoveExit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not SentenceRow row) return;
        _exit.Remove(row);
        UpdateDescription();
    }

    private void SaveStrategy_Click(object sender, RoutedEventArgs e)
    {
        var strategy = BuildStrategyFromUi();

        // Edit mode: keep the original StrategyId so the same file is updated.
        if (_editing is not null) strategy.StrategyId = _editing.StrategyId;
        else strategy.StrategyId = Guid.NewGuid().ToString("N");

        // Copy mode: new id (already generated) + "(Copy)" name suffix.
        if (_isCopy && !strategy.Name.EndsWith("(Copy)")) strategy.Name += " (Copy)";

        // Unique name: append (2), (3), ... if needed (excluding the strategy being edited)
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

    private static RuleGroup BuildGroup(string? mode, string? minScoreText, ObservableCollection<SentenceRow> rows)
    {
        return new RuleGroup
        {
            Mode = mode ?? "all",
            MinScore = minScoreText is not null && double.TryParse(minScoreText, out var s) ? s : null,
            TriggerMode = "onTransition",
            Conditions = rows.Select(r => (ConditionNode)new LeafCondition
            {
                Left = r.Left,
                Op = r.Op,
                Right = r.Right == NumberToken
                    ? (double.TryParse(r.Number, out var n) ? n : 0d)
                    : r.Right,
                Weight = r.Weight
            }).ToList()
        };
    }
}