using System.Collections.Generic;
using Meowgnal.Models;

namespace Meowgnal.Services;

public static class StrategyTemplateCatalog
{
    public static List<StrategyTemplate> GetAll()
    {
        return new List<StrategyTemplate>
        {
            new StrategyTemplate
            {
                Name = "Fast Trend Rider",
                Description = "Quick trend following with EMA 9/21 crossover.",
                Style = "Trend",
                DefaultTimeframe = "1h",
                RiskLevel = "Medium",
                BuildStrategy = symbol => new StrategyDefinition
                {
                    Name = "Fast Trend Rider",
                    Symbol = symbol,
                    Timeframe = "1h",
                    DataSource = "binance",
                    Indicators = new()
                    {
                        new IndicatorDefinition { Id = "ema9", Type = "EMA", Params = new() { ["period"] = 9 } },
                        new IndicatorDefinition { Id = "ema21", Type = "EMA", Params = new() { ["period"] = 21 } }
                    },
                    EntryRules = new RuleGroup
                    {
                        Mode = "all",
                        Conditions = new()
                        {
                            new LeafCondition { Left = "ema9", Op = "crossesAbove", Right = "ema21", Weight = 1 }
                        }
                    },
                    ExitRules = new RuleGroup
                    {
                        Mode = "any",
                        Conditions = new()
                        {
                            new LeafCondition { Left = "ema9", Op = "crossesBelow", Right = "ema21", Weight = 1 }
                        }
                    },
                    RiskManagement = new RiskManagementConfig
                    {
                        StopLoss = new StopLossConfig { Method = "fixedPercent", Multiplier = 2 },
                        Target = new TargetConfig { Method = "fixedPercent", Value = 4 }
                    }
                }
            },

            new StrategyTemplate
            {
                Name = "Golden Cross",
                Description = "Classic long-term trend with SMA 50/200.",
                Style = "Trend",
                DefaultTimeframe = "1d",
                RiskLevel = "Low",
                BuildStrategy = symbol => new StrategyDefinition
                {
                    Name = "Golden Cross",
                    Symbol = symbol,
                    Timeframe = "1d",
                    DataSource = "binance",
                    Indicators = new()
                    {
                        new IndicatorDefinition { Id = "sma50", Type = "SMA", Params = new() { ["period"] = 50 } },
                        new IndicatorDefinition { Id = "sma200", Type = "SMA", Params = new() { ["period"] = 200 } }
                    },
                    EntryRules = new RuleGroup
                    {
                        Mode = "all",
                        Conditions = new()
                        {
                            new LeafCondition { Left = "sma50", Op = "crossesAbove", Right = "sma200", Weight = 1 }
                        }
                    },
                    ExitRules = new RuleGroup
                    {
                        Mode = "any",
                        Conditions = new()
                        {
                            new LeafCondition { Left = "sma50", Op = "crossesBelow", Right = "sma200", Weight = 1 }
                        }
                    },
                    RiskManagement = new RiskManagementConfig
                    {
                        StopLoss = new StopLossConfig { Method = "fixedPercent", Multiplier = 1.5 },
                        Target = new TargetConfig { Method = "fixedPercent", Value = 3 }
                    }
                }
            },

            new StrategyTemplate
            {
                Name = "RSI Dip Buyer",
                Description = "Buy oversold dips, sell when overbought.",
                Style = "Reversal",
                DefaultTimeframe = "4h",
                RiskLevel = "Medium",
                BuildStrategy = symbol => new StrategyDefinition
                {
                    Name = "RSI Dip Buyer",
                    Symbol = symbol,
                    Timeframe = "4h",
                    DataSource = "binance",
                    Indicators = new()
                    {
                        new IndicatorDefinition { Id = "rsi14", Type = "RSI", Params = new() { ["period"] = 14 } }
                    },
                    EntryRules = new RuleGroup
                    {
                        Mode = "all",
                        Conditions = new()
                        {
                            new LeafCondition { Left = "rsi14", Op = "lessThan", Right = 30, Weight = 1 }
                        }
                    },
                    ExitRules = new RuleGroup
                    {
                        Mode = "any",
                        Conditions = new()
                        {
                            new LeafCondition { Left = "rsi14", Op = "greaterThan", Right = 70, Weight = 1 }
                        }
                    },
                    RiskManagement = new RiskManagementConfig
                    {
                        StopLoss = new StopLossConfig { Method = "fixedPercent", Multiplier = 2 },
                        Target = new TargetConfig { Method = "fixedPercent", Value = 4 }
                    }
                }
            },

            new StrategyTemplate
            {
                Name = "MACD Momentum",
                Description = "Momentum trades with MACD crossover.",
                Style = "Trend",
                DefaultTimeframe = "1h",
                RiskLevel = "Medium",
                BuildStrategy = symbol => new StrategyDefinition
                {
                    Name = "MACD Momentum",
                    Symbol = symbol,
                    Timeframe = "1h",
                    DataSource = "binance",
                    Indicators = new()
                    {
                        new IndicatorDefinition { Id = "macd", Type = "MACD", Params = new() { ["fastPeriod"] = 12, ["slowPeriod"] = 26, ["signalPeriod"] = 9 } }
                    },
                    EntryRules = new RuleGroup
                    {
                        Mode = "all",
                        Conditions = new()
                        {
                            new LeafCondition { Left = "macd", Op = "crossesAbove", Right = "signal", Weight = 1 }
                        }
                    },
                    ExitRules = new RuleGroup
                    {
                        Mode = "any",
                        Conditions = new()
                        {
                            new LeafCondition { Left = "macd", Op = "crossesBelow", Right = "signal", Weight = 1 }
                        }
                    },
                    RiskManagement = new RiskManagementConfig
                    {
                        StopLoss = new StopLossConfig { Method = "fixedPercent", Multiplier = 2 },
                        Target = new TargetConfig { Method = "fixedPercent", Value = 4 }
                    }
                }
            },

            new StrategyTemplate
            {
                Name = "Cautious Combo",
                Description = "Trend with RSI filter to avoid false signals.",
                Style = "Trend",
                DefaultTimeframe = "4h",
                RiskLevel = "Low",
                BuildStrategy = symbol => new StrategyDefinition
                {
                    Name = "Cautious Combo",
                    Symbol = symbol,
                    Timeframe = "4h",
                    DataSource = "binance",
                    Indicators = new()
                    {
                        new IndicatorDefinition { Id = "ema20", Type = "EMA", Params = new() { ["period"] = 20 } },
                        new IndicatorDefinition { Id = "ema50", Type = "EMA", Params = new() { ["period"] = 50 } },
                        new IndicatorDefinition { Id = "rsi14", Type = "RSI", Params = new() { ["period"] = 14 } }
                    },
                    EntryRules = new RuleGroup
                    {
                        Mode = "all",
                        Conditions = new()
                        {
                            new LeafCondition { Left = "ema20", Op = "crossesAbove", Right = "ema50", Weight = 1 },
                            new LeafCondition { Left = "rsi14", Op = "lessThan", Right = 60, Weight = 1 }
                        }
                    },
                    ExitRules = new RuleGroup
                    {
                        Mode = "any",
                        Conditions = new()
                        {
                            new LeafCondition { Left = "ema20", Op = "crossesBelow", Right = "ema50", Weight = 1 }
                        }
                    },
                    RiskManagement = new RiskManagementConfig
                    {
                        StopLoss = new StopLossConfig { Method = "fixedPercent", Multiplier = 1.5 },
                        Target = new TargetConfig { Method = "fixedPercent", Value = 3 }
                    }
                }
            },

            new StrategyTemplate
            {
                Name = "ATR Volatility Breakout",
                Description = "Catch breakouts with ATR-based stops.",
                Style = "Volatility",
                DefaultTimeframe = "1h",
                RiskLevel = "High",
                BuildStrategy = symbol => new StrategyDefinition
                {
                    Name = "ATR Volatility Breakout",
                    Symbol = symbol,
                    Timeframe = "1h",
                    DataSource = "binance",
                    Indicators = new()
                    {
                        new IndicatorDefinition { Id = "sma20", Type = "SMA", Params = new() { ["period"] = 20 } },
                        new IndicatorDefinition { Id = "atr14", Type = "ATR", Params = new() { ["period"] = 14 } }
                    },
                    EntryRules = new RuleGroup
                    {
                        Mode = "all",
                        Conditions = new()
                        {
                            new LeafCondition { Left = "price", Op = "crossesAbove", Right = "sma20", Weight = 1 }
                        }
                    },
                    ExitRules = new RuleGroup
                    {
                        Mode = "any",
                        Conditions = new()
                        {
                            new LeafCondition { Left = "price", Op = "crossesBelow", Right = "sma20", Weight = 1 }
                        }
                    },
                    RiskManagement = new RiskManagementConfig
                    {
                        StopLoss = new StopLossConfig { Method = "ATR", Multiplier = 2 },
                        Target = new TargetConfig { Method = "ATR", Value = 4 }
                    }
                }
            }
        };
    }
}