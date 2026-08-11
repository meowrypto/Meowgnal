using System;
using System.Collections.Generic;
using Meowgnal.Models;

namespace Meowgnal.Services;

/// <summary>Builds a complete strategy from the 3-question wizard answers.</summary>
public static class StrategyWizardService
{
    public static StrategyDefinition Build(string style, string speed, string caution, string symbol)
    {
        // Speed decides the timeframe and indicator periods
        string timeframe;
        int fastPeriod, slowPeriod, maPeriod;
        switch (speed)
        {
            case "Scalp":
                timeframe = "15m"; fastPeriod = 9; slowPeriod = 21; maPeriod = 20; break;
            case "Long-term":
                timeframe = "1d"; fastPeriod = 50; slowPeriod = 200; maPeriod = 200; break;
            default: // Swing
                timeframe = "1h"; fastPeriod = 20; slowPeriod = 50; maPeriod = 50; break;
        }

        // Caution decides the risk plan and the extra RSI filter
        double slPercent;
        double rr;
        bool useRsiFilter;
        switch (caution)
        {
            case "Careful":
                slPercent = 1.5; rr = 2; useRsiFilter = true; break;
            case "Aggressive":
                slPercent = 3; rr = 3; useRsiFilter = false; break;
            default: // Balanced
                slPercent = 2; rr = 2; useRsiFilter = false; break;
        }

        var strategy = new StrategyDefinition
        {
            StrategyId = Guid.NewGuid().ToString("N"),
            Name = $"Wizard {style} {speed} {caution}",
            Symbol = symbol,
            Timeframe = timeframe,
            DataSource = "binance",
            RiskManagement = new RiskManagementConfig
            {
                StopLoss = new StopLossConfig { Method = "fixedPercent", Multiplier = slPercent },
                Target = new TargetConfig { Method = "riskRewardRatio", Value = rr }
            }
        };

        switch (style)
        {
            case "Reversal":
                {
                    strategy.Indicators.Add(new IndicatorDefinition { Id = "rsi14", Type = "RSI", Params = new() { ["period"] = 14 } });
                    strategy.EntryRules = new RuleGroup
                    {
                        Mode = "all",
                        Conditions = new()
                    {
                        new LeafCondition { Left = "rsi14", Op = "lessThan", Right = caution == "Careful" ? 25d : 30d, Weight = 1 }
                    }
                    };
                    strategy.ExitRules = new RuleGroup
                    {
                        Mode = "any",
                        Conditions = new()
                    {
                        new LeafCondition { Left = "rsi14", Op = "greaterThan", Right = 70d, Weight = 1 }
                    }
                    };
                    break;
                }
            case "Breakout":
                {
                    strategy.Indicators.Add(new IndicatorDefinition { Id = "ma", Type = "SMA", Params = new() { ["period"] = maPeriod } });
                    if (useRsiFilter)
                        strategy.Indicators.Add(new IndicatorDefinition { Id = "rsi14", Type = "RSI", Params = new() { ["period"] = 14 } });

                    var entryConds = new List<ConditionNode>
                {
                    new LeafCondition { Left = "price", Op = "crossesAbove", Right = "ma", Weight = 1 }
                };
                    if (useRsiFilter)
                        entryConds.Add(new LeafCondition { Left = "rsi14", Op = "lessThan", Right = 60d, Weight = 1 });

                    strategy.EntryRules = new RuleGroup { Mode = "all", Conditions = entryConds };
                    strategy.ExitRules = new RuleGroup
                    {
                        Mode = "any",
                        Conditions = new()
                    {
                        new LeafCondition { Left = "price", Op = "crossesBelow", Right = "ma", Weight = 1 }
                    }
                    };
                    break;
                }
            default: // Trend
                {
                    var maType = speed == "Long-term" ? "SMA" : "EMA";
                    strategy.Indicators.Add(new IndicatorDefinition { Id = "fast", Type = maType, Params = new() { ["period"] = fastPeriod } });
                    strategy.Indicators.Add(new IndicatorDefinition { Id = "slow", Type = maType, Params = new() { ["period"] = slowPeriod } });
                    if (useRsiFilter)
                        strategy.Indicators.Add(new IndicatorDefinition { Id = "rsi14", Type = "RSI", Params = new() { ["period"] = 14 } });

                    var entryConds = new List<ConditionNode>
                {
                    new LeafCondition { Left = "fast", Op = "crossesAbove", Right = "slow", Weight = 1 }
                };
                    if (useRsiFilter)
                        entryConds.Add(new LeafCondition { Left = "rsi14", Op = "lessThan", Right = 60d, Weight = 1 });

                    strategy.EntryRules = new RuleGroup { Mode = "all", Conditions = entryConds };
                    strategy.ExitRules = new RuleGroup
                    {
                        Mode = "any",
                        Conditions = new()
                    {
                        new LeafCondition { Left = "fast", Op = "crossesBelow", Right = "slow", Weight = 1 }
                    }
                    };
                    break;
                }
        }

        return strategy;
    }
}