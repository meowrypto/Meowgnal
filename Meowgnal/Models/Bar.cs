using System;

namespace Meowgnal.Models;

// One candlestick (OHLCV) for a given symbol/timeframe.
public sealed class Bar
{
    public DateTimeOffset OpenTime { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
}