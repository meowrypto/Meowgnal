using System;
using System.Text.Json.Serialization;
using FacioQuo.Stock.Indicators;

namespace Meowgnal.Models;

// Implements IBar so this same class can be passed directly into
// FacioQuo's indicator functions (ToEma, ToRsi, ...) with zero conversion.
public sealed class Bar : IBar
{
    public DateTime Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }

    // Required by IReusable (which IBar extends) — enables chaining
    // indicators together. We expose Close as the reusable value.
    [JsonIgnore]
    public double Value => (double)Close;
}