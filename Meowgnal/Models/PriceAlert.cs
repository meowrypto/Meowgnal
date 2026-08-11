using System;
using System.Collections.Generic;

namespace Meowgnal.Models;

/// <summary>A one-shot price-cross alert created from the chart context menu.</summary>
public sealed class PriceAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Symbol { get; set; } = "";
    public string DataSource { get; set; } = "binance";
    public decimal Price { get; set; }

    /// <summary>Null until the first live check; then tracks which side price is on.</summary>
    public bool? WasAbove { get; set; } = null;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PriceAlertsFile
{
    public List<PriceAlert> Alerts { get; set; } = new();
}