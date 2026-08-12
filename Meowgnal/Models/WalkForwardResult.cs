using System.Collections.Generic;

namespace Meowgnal.Models;

public class WalkForwardResult
{
    public List<BacktestResult> InSampleResults { get; set; } = [];
    public List<BacktestResult> OutOfSampleResults { get; set; } = [];
    public BacktestResult AggregateInSample { get; set; } = new();
    public BacktestResult AggregateOutOfSample { get; set; } = new();
    public bool IsOverfit { get; set; }
    public string OverfitReason { get; set; } = string.Empty;
}