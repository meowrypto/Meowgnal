using System.Collections.Generic;

namespace Meowgnal.Models;

// Input parameters for Monte Carlo simulation.
public sealed class MonteCarloInput
{
    // Profit/loss of each real trade from backtest (as percentage, e.g. 2.5 = +2.5%, -1.2 = -1.2%).
    public List<decimal> TradeReturns { get; set; } = new();

    public decimal StartingBalance { get; set; } = 10000m;

    // How many simulated futures to generate (typically 1000-5000).
    public int SimulationCount { get; set; } = 1000;

    // How many trades to simulate per future (can be larger than real trade count
    // to model "what if the strategy continues").
    public int TradesPerSimulation { get; set; }

    // True = classic bootstrap (each trade can be picked multiple times).
    // False = sample without replacement (rarely used, mostly for comparison).
    public bool WithReplacement { get; set; } = true;

    // For reproducibility: null = random seed, non-null = fixed seed.
    public int? RandomSeed { get; set; } = null;

    // Block bootstrap infrastructure (for future development):
    // BlockSize > 1 means sample contiguous blocks of BlockSize trades
    // to preserve streaks of wins/losses. Default = 1 (no blocking).
    public int BlockSize { get; set; } = 1;

    // Account is considered "ruined" if balance drops below this fraction of starting balance.
    // Default = 0.10 (10% of starting balance).
    public decimal RuinThresholdFraction { get; set; } = 0.10m;
}

// Output of a Monte Carlo simulation run.
public sealed class MonteCarloResult
{
    // All simulated equity curves (one list per simulation).
    public List<List<decimal>> AllEquityCurves { get; set; } = new();

    // Final balance statistics across all simulations.
    public decimal MedianFinalBalance { get; set; }
    public decimal P5FinalBalance { get; set; }    // 5th percentile (worst realistic case)
    public decimal P95FinalBalance { get; set; }   // 95th percentile (best realistic case)

    // Drawdown statistics across all simulations.
    public decimal MedianMaxDrawdown { get; set; }
    public decimal WorstCaseMaxDrawdown { get; set; }  // 95th percentile of drawdowns

    // Fraction of simulations where balance dropped below ruin threshold.
    public double RuinProbability { get; set; }

    // For plotting: at each trade step (0 to TradesPerSimulation), the percentile
    // values across all simulations. These form the "cone of uncertainty" on the chart.
    public List<decimal> EquityCurveP5 { get; set; } = new();
    public List<decimal> EquityCurveP50 { get; set; } = new();
    public List<decimal> EquityCurveP95 { get; set; } = new();

    // Summary numbers for quick display.
    public int TotalSimulations { get; set; }
    public int TradesPerSimulation { get; set; }
    public bool BlockBootstrapUsed { get; set; }
}