using System.Collections.Generic;

namespace Meowgnal.Services;

/// <summary>Metadata for one indicator in the registry (UI-friendly).</summary>
public sealed class IndicatorInfo
{
    public string Type { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public int DefaultPeriod { get; set; } = 14;
}

/// <summary>
/// The indicator registry (v1). FacioQuo has 100+ indicators;
/// new ones are added here gradually in future versions.
/// </summary>
public static class IndicatorRegistry
{
    public static readonly List<IndicatorInfo> All = new()
    {
        new IndicatorInfo
        {
            Type = "EMA",
            Label = "EMA — Exponential Moving Average",
            Description = "Average of recent prices that reacts faster to new prices.",
            DefaultPeriod = 9
        },
        new IndicatorInfo
        {
            Type = "SMA",
            Label = "SMA — Simple Moving Average",
            Description = "Plain average of the last N closing prices.",
            DefaultPeriod = 20
        },
        new IndicatorInfo
        {
            Type = "RSI",
            Label = "RSI — Relative Strength Index",
            Description = "0..100 oscillator; below 30 = oversold, above 70 = overbought.",
            DefaultPeriod = 14
        },
        new IndicatorInfo
        {
            Type = "MACD",
            Label = "MACD — Momentum (line vs signal)",
            Description = "Momentum indicator; compare its line against its signal line.",
            DefaultPeriod = 12
        },
        new IndicatorInfo
        {
            Type = "ATR",
            Label = "ATR — Average True Range",
            Description = "Measures how much the market moves on average (volatility).",
            DefaultPeriod = 14
        }
    };
}