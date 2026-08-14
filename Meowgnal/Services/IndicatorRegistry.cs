using System.Collections.Generic;

namespace Meowgnal.Services;

/// <summary>Metadata for one indicator in the registry (UI-friendly).</summary>
public sealed class IndicatorInfo
{
    public string Type { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public int DefaultPeriod { get; set; } = 14;

    // Indicators with multiple output series (e.g. Bollinger Bands → upper/middle/lower).
    // When set, the builder shows one token per sub-output (id + "." + subId).
    public string[]? SubOutputs { get; set; }

    // When true, the UI hides the period field because this indicator has no period parameter.
    public bool HasNoPeriod { get; set; }
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
        },
        // Bollinger Bands — three outputs (upper / middle / lower).
        // Each instance becomes 3 usable tokens (e.g. bb1.upper, bb1.middle, bb1.lower).
        new IndicatorInfo
        {
            Type = "BBANDS",
            Label = "BB — Bollinger Bands (upper / middle / lower)",
            Description = "Volatility bands around a moving average. Price touching the lower band often means oversold; the upper band often means overbought.",
            DefaultPeriod = 20,
            SubOutputs = new[] { "upper", "middle", "lower" }
        },
        new IndicatorInfo
        {
            Type = "STOCH",
            Label = "STOCH — Stochastic Oscillator",
            Description = "0..100 oscillator comparing the close to the recent range. Below 20 = oversold, above 80 = overbought.",
            DefaultPeriod = 14
        },
        new IndicatorInfo
        {
            Type = "ADX",
            Label = "ADX — Average Directional Index",
            Description = "Measures trend strength (0..100). Above 25 means a strong trend; below 20 means a sideways market.",
            DefaultPeriod = 14
        },
        new IndicatorInfo
        {
            Type = "VOLSMA",
            Label = "VOLSMA — Volume Moving Average",
            Description = "Average volume over the last N bars. Useful to confirm a breakout with above-average volume.",
            DefaultPeriod = 20
        },
        new IndicatorInfo
        {
            Type = "VWAP",
            Label = "VWAP — Volume Weighted Average Price",
            Description = "Average price weighted by volume. Price above VWAP is often seen as bullish; below VWAP as bearish.",
            DefaultPeriod = 20,
            HasNoPeriod = true
        }
    };
}