using System.Collections.Generic;

namespace Meowgnal.Services;

public sealed class IndicatorInfo
{
    public string Type { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public int DefaultPeriod { get; set; } = 14;
    public string SubCategory { get; set; } = "Other";
    public string[]? SubOutputs { get; set; }
    public bool HasNoPeriod { get; set; }
    public bool IsFundamental { get; set; }
    public string? LimitationNote { get; set; }
}

public static class IndicatorRegistry
{
    public static readonly List<IndicatorInfo> All = new()
    {
        // === MOVING AVERAGES ===
        new IndicatorInfo { Type = "SMA", Label = "SMA — Simple Moving Average", Description = "Plain average of the last N closing prices.", DefaultPeriod = 20, SubCategory = "Moving Averages" },
        new IndicatorInfo { Type = "EMA", Label = "EMA — Exponential Moving Average", Description = "Average that reacts faster to recent prices.", DefaultPeriod = 9, SubCategory = "Moving Averages" },
        new IndicatorInfo { Type = "WMA", Label = "WMA — Weighted Moving Average", Description = "Average giving more weight to recent prices.", DefaultPeriod = 20, SubCategory = "Moving Averages" },
        new IndicatorInfo { Type = "HMA", Label = "HMA — Hull Moving Average", Description = "Smooth and responsive moving average.", DefaultPeriod = 20, SubCategory = "Moving Averages" },
        new IndicatorInfo { Type = "DEMA", Label = "DEMA — Double EMA", Description = "Double exponential moving average for faster signals.", DefaultPeriod = 20, SubCategory = "Moving Averages" },
        new IndicatorInfo { Type = "TEMA", Label = "TEMA — Triple EMA", Description = "Triple exponential moving average, even faster than DEMA.", DefaultPeriod = 20, SubCategory = "Moving Averages" },
        new IndicatorInfo { Type = "KAMA", Label = "KAMA — Kaufman Adaptive MA", Description = "Adaptive MA that adjusts to market volatility.", DefaultPeriod = 10, SubCategory = "Moving Averages" },
        new IndicatorInfo { Type = "VWMA", Label = "VWMA — Volume Weighted MA", Description = "Moving average weighted by volume.", DefaultPeriod = 20, SubCategory = "Moving Averages" },

        // === OSCILLATORS ===
        new IndicatorInfo { Type = "RSI", Label = "RSI — Relative Strength Index", Description = "0..100 oscillator; below 30 = oversold, above 70 = overbought.", DefaultPeriod = 14, SubCategory = "Oscillators" },
        new IndicatorInfo { Type = "STOCH", Label = "STOCH — Stochastic Oscillator", Description = "0..100 oscillator comparing close to recent range. Below 20 = oversold, above 80 = overbought.", DefaultPeriod = 14, SubCategory = "Oscillators" },
        new IndicatorInfo { Type = "STOCHRSI", Label = "StochRSI — Stochastic RSI", Description = "Stochastic applied to RSI; more sensitive than RSI alone.", DefaultPeriod = 14, SubCategory = "Oscillators" },
        new IndicatorInfo { Type = "CCI", Label = "CCI — Commodity Channel Index", Description = "Measures deviation from average price. Above +100 = overbought, below -100 = oversold.", DefaultPeriod = 20, SubCategory = "Oscillators" },
        new IndicatorInfo { Type = "WILLIAMSR", Label = "Williams %R", Description = "0 to -100 oscillator; above -20 = overbought, below -80 = oversold.", DefaultPeriod = 14, SubCategory = "Oscillators" },
        new IndicatorInfo { Type = "MFI", Label = "MFI — Money Flow Index", Description = "RSI weighted by volume; 0..100 oscillator.", DefaultPeriod = 14, SubCategory = "Oscillators" },
        new IndicatorInfo { Type = "ROC", Label = "ROC — Rate of Change", Description = "Percentage change over N periods.", DefaultPeriod = 10, SubCategory = "Oscillators" },
        new IndicatorInfo { Type = "TRIX", Label = "TRIX — Triple Smoothed EMA", Description = "Momentum oscillator showing percent change of triple-smoothed EMA.", DefaultPeriod = 15, SubCategory = "Oscillators" },
        new IndicatorInfo { Type = "ULTIMATE", Label = "Ultimate Oscillator", Description = "Combines short, medium, and long timeframes into one oscillator.", DefaultPeriod = 28, SubCategory = "Oscillators" },
        new IndicatorInfo { Type = "AO", Label = "Awesome Oscillator", Description = "Difference between 34-period and 5-period SMAs of midpoints.", DefaultPeriod = 34, SubCategory = "Oscillators", HasNoPeriod = true },
        new IndicatorInfo { Type = "CMO", Label = "CMO — Chande Momentum", Description = "Momentum oscillator; above 50 = overbought, below -50 = oversold.", DefaultPeriod = 14, SubCategory = "Oscillators" },
        new IndicatorInfo { Type = "CONNORSRSI", Label = "Connors RSI", Description = "Mean-reversion indicator combining RSI, streak, and percent rank.", DefaultPeriod = 14, SubCategory = "Oscillators" },
        new IndicatorInfo { Type = "MACD", Label = "MACD — Momentum", Description = "Momentum indicator; compare line against signal line.", DefaultPeriod = 12, SubCategory = "Oscillators" },
        new IndicatorInfo { Type = "ADX", Label = "ADX — Average Directional Index", Description = "Measures trend strength (0..100). Above 25 = strong trend.", DefaultPeriod = 14, SubCategory = "Oscillators" },

        // === VOLATILITY ===
        new IndicatorInfo { Type = "ATR", Label = "ATR — Average True Range", Description = "Measures how much the market moves on average (volatility).", DefaultPeriod = 14, SubCategory = "Volatility" },
        new IndicatorInfo { Type = "BBANDS", Label = "BB — Bollinger Bands", Description = "Volatility bands around a moving average. Touching lower band = oversold; upper = overbought.", DefaultPeriod = 20, SubCategory = "Volatility", SubOutputs = new[] { "upper", "middle", "lower" } },
        new IndicatorInfo { Type = "KELTNER", Label = "Keltner Channel", Description = "Volatility envelope using ATR around an EMA.", DefaultPeriod = 20, SubCategory = "Volatility", SubOutputs = new[] { "upper", "middle", "lower" } },
        new IndicatorInfo { Type = "DONCHIAN", Label = "Donchian Channel", Description = "Highest high and lowest low over N periods.", DefaultPeriod = 20, SubCategory = "Volatility", SubOutputs = new[] { "upper", "middle", "lower" } },
        new IndicatorInfo { Type = "STDDEV", Label = "Standard Deviation", Description = "Measures price dispersion from the mean.", DefaultPeriod = 20, SubCategory = "Volatility" },
        new IndicatorInfo { Type = "ULCER", Label = "Ulcer Index", Description = "Measures downside risk (depth and duration of drawdowns).", DefaultPeriod = 14, SubCategory = "Volatility" },

        // === VOLUME ===
        new IndicatorInfo { Type = "VOLSMA", Label = "VOLSMA — Volume Moving Average", Description = "Average volume over the last N bars. Confirms breakouts.", DefaultPeriod = 20, SubCategory = "Volume" },
        new IndicatorInfo { Type = "VWAP", Label = "VWAP — Volume Weighted Average Price", Description = "Average price weighted by volume. Above VWAP = bullish.", DefaultPeriod = 20, SubCategory = "Volume", HasNoPeriod = true },
        new IndicatorInfo { Type = "OBV", Label = "OBV — On-Balance Volume", Description = "Cumulative volume indicator; rising OBV confirms uptrend.", DefaultPeriod = 20, SubCategory = "Volume", HasNoPeriod = true },
        new IndicatorInfo { Type = "CMF", Label = "CMF — Chaikin Money Flow", Description = "Measures accumulation/distribution over N periods. Above 0 = buying pressure.", DefaultPeriod = 20, SubCategory = "Volume" },
        new IndicatorInfo { Type = "FORCEINDEX", Label = "Force Index", Description = "Combines price change and volume to measure buying/selling pressure.", DefaultPeriod = 13, SubCategory = "Volume" },
        new IndicatorInfo { Type = "ADL", Label = "A/D Line — Accumulation/Distribution", Description = "Cumulative volume indicator showing money flow.", DefaultPeriod = 20, SubCategory = "Volume", HasNoPeriod = true },

        // === TREND ===
        new IndicatorInfo { Type = "AROON", Label = "Aroon", Description = "Measures trend strength and identifies trend changes. 0..100.", DefaultPeriod = 25, SubCategory = "Trend", SubOutputs = new[] { "up", "down" } },
        new IndicatorInfo { Type = "SAR", Label = "Parabolic SAR", Description = "Dots above/below price indicating trend direction and reversals.", DefaultPeriod = 14, SubCategory = "Trend", HasNoPeriod = true },
        new IndicatorInfo { Type = "SUPERTREND", Label = "SuperTrend", Description = "Trend-following indicator based on ATR. Price above line = bullish.", DefaultPeriod = 10, SubCategory = "Trend" },
        new IndicatorInfo { Type = "ICHIMOKU", Label = "Ichimoku Cloud", Description = "Complete trend system: Tenkan, Kijun, Senkou A/B, Chikou.", DefaultPeriod = 9, SubCategory = "Trend", SubOutputs = new[] { "tenkan", "kijun", "senkouA", "senkouB", "chikou" } },
        new IndicatorInfo { Type = "VORTEX", Label = "Vortex Indicator", Description = "Identifies trend start and direction. VI+ vs VI-.", DefaultPeriod = 14, SubCategory = "Trend", SubOutputs = new[] { "plus", "minus" } },
                new IndicatorInfo { Type = "CHOP", Label = "Choppiness Index", Description = "Measures if market is trending or choppy. Above 61.8 = choppy.", DefaultPeriod = 14, SubCategory = "Trend" },

        // === FUNDAMENTAL (external data, not derived from candles) ===
        new IndicatorInfo { Type = "FEARGREED", Label = "Fear & Greed Index", Description = "Market sentiment 0..100 (Extreme Fear → Extreme Greed). Daily.", DefaultPeriod = 0, SubCategory = "Fundamental", HasNoPeriod = true, IsFundamental = true },
        new IndicatorInfo { Type = "BTCDOM", Label = "BTC Dominance", Description = "Bitcoin's share of total crypto market cap (percent).", DefaultPeriod = 0, SubCategory = "Fundamental", HasNoPeriod = true, IsFundamental = true, LimitationNote = "Limited historical data for backtesting" },
        new IndicatorInfo { Type = "FUNDING", Label = "Funding Rate", Description = "Perpetual swap funding rate. Positive = longs pay shorts.", DefaultPeriod = 0, SubCategory = "Fundamental", HasNoPeriod = true, IsFundamental = true },
        new IndicatorInfo { Type = "OI", Label = "Open Interest", Description = "Total open contracts (USD). Rising OI = new money flowing in.", DefaultPeriod = 0, SubCategory = "Fundamental", HasNoPeriod = true, IsFundamental = true, LimitationNote = "Limited history on Hyperliquid" },
    };
};