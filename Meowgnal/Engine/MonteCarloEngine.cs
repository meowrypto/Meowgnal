using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Meowgnal.Models;

namespace Meowgnal.Engine;

// Monte Carlo simulation engine for analyzing backtest results.
// Generates thousands of possible futures by resampling real trade returns,
// then computes statistics on final balances, drawdowns, and ruin probability.
public static class MonteCarloEngine
{
    // Runs the full Monte Carlo simulation asynchronously (does not block UI thread).
    public static Task<MonteCarloResult> RunAsync(MonteCarloInput input, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Run(input, cancellationToken), cancellationToken);
    }

    // Synchronous version for testing or simple scenarios.
    public static MonteCarloResult Run(MonteCarloInput input, CancellationToken cancellationToken = default)
    {
        if (input.TradeReturns.Count == 0)
            return new MonteCarloResult { TotalSimulations = 0, TradesPerSimulation = 0 };

        var tradesPerSim = input.TradesPerSimulation > 0 ? input.TradesPerSimulation : input.TradeReturns.Count;
        var rng = input.RandomSeed.HasValue ? new Random(input.RandomSeed.Value) : new Random();
        var ruinThreshold = input.StartingBalance * input.RuinThresholdFraction;

        // Storage for all simulation outcomes
        var allEquityCurves = new List<List<decimal>>(input.SimulationCount);
        var finalBalances = new decimal[input.SimulationCount];
        var maxDrawdowns = new double[input.SimulationCount];
        var ruinedCount = 0;

        // Run each simulation
        for (int sim = 0; sim < input.SimulationCount; sim++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // Sample trades for this simulation
            var sampledReturns = SampleReturns(input.TradeReturns, tradesPerSim, input.WithReplacement, input.BlockSize, rng);

            // Build equity curve and track metrics
            var equityCurve = new List<decimal>(tradesPerSim + 1);
            var balance = input.StartingBalance;
            equityCurve.Add(balance);

            var peakBalance = balance;
            var maxDrawdown = 0.0;
            var isRuined = false;

            foreach (var ret in sampledReturns)
            {
                // Apply return: if ret is percentage (e.g., 2.5 means +2.5%), convert to multiplier
                var multiplier = 1m + (ret / 100m);
                balance *= multiplier;

                equityCurve.Add(balance);

                // Track drawdown
                if (balance > peakBalance) peakBalance = balance;
                var dd = peakBalance > 0 ? (double)((peakBalance - balance) / peakBalance * 100m) : 0;
                if (dd > maxDrawdown) maxDrawdown = dd;

                // Check ruin
                if (!isRuined && balance <= ruinThreshold) isRuined = true;
            }

            allEquityCurves.Add(equityCurve);
            finalBalances[sim] = balance;
            maxDrawdowns[sim] = maxDrawdown;
            if (isRuined) ruinedCount++;
        }

        // Compute percentiles on final balances
        Array.Sort(finalBalances);
        var p5Final = Percentile(finalBalances, 0.05);
        var p50Final = Percentile(finalBalances, 0.50);
        var p95Final = Percentile(finalBalances, 0.95);

        // Compute percentiles on max drawdowns
        Array.Sort(maxDrawdowns);
        var medianDD = Percentile(maxDrawdowns, 0.50);
        var p95DD = Percentile(maxDrawdowns, 0.95);  // 95th percentile = worst realistic drawdown

        // Build percentile equity curves (P5, P50, P95 at each step)
        var (p5Curve, p50Curve, p95Curve) = BuildPercentileCurves(allEquityCurves, tradesPerSim + 1);

        return new MonteCarloResult
        {
            AllEquityCurves = allEquityCurves,
            MedianFinalBalance = p50Final,
            P5FinalBalance = p5Final,
            P95FinalBalance = p95Final,
            MedianMaxDrawdown = (decimal)medianDD,
            WorstCaseMaxDrawdown = (decimal)p95DD,
            RuinProbability = (double)ruinedCount / input.SimulationCount * 100.0,
            EquityCurveP5 = p5Curve,
            EquityCurveP50 = p50Curve,
            EquityCurveP95 = p95Curve,
            TotalSimulations = input.SimulationCount,
            TradesPerSimulation = tradesPerSim,
            BlockBootstrapUsed = input.BlockSize > 1
        };
    }

    // Samples trade returns according to bootstrap strategy.
    private static List<decimal> SampleReturns(
        List<decimal> returns, int count, bool withReplacement, int blockSize, Random rng)
    {
        if (blockSize > 1)
        {
            // Block bootstrap: sample contiguous blocks to preserve win/loss streaks.
            // TODO: Not yet wired to UI. Infrastructure is ready for future development.
            var result = new List<decimal>(count);
            var numBlocks = (count + blockSize - 1) / blockSize;  // Ceiling division
            for (int i = 0; i < numBlocks && result.Count < count; i++)
            {
                var startIdx = rng.Next(0, returns.Count - blockSize + 1);
                for (int j = 0; j < blockSize && result.Count < count; j++)
                    result.Add(returns[startIdx + j]);
            }
            return result;
        }

        // Classic bootstrap: sample individual trades (with or without replacement).
        var sampled = new List<decimal>(count);
        if (withReplacement)
        {
            for (int i = 0; i < count; i++)
                sampled.Add(returns[rng.Next(returns.Count)]);
        }
        else
        {
            // Without replacement: shuffle and take first 'count' elements (wrap if needed)
            var shuffled = returns.OrderBy(_ => rng.Next()).ToList();
            for (int i = 0; i < count; i++)
                sampled.Add(shuffled[i % shuffled.Count]);
        }
        return sampled;
    }

    // Builds percentile curves across all simulations at each time step.
    private static (List<decimal> P5, List<decimal> P50, List<decimal> P95) BuildPercentileCurves(
        List<List<decimal>> allCurves, int steps)
    {
        var p5 = new List<decimal>(steps);
        var p50 = new List<decimal>(steps);
        var p95 = new List<decimal>(steps);

        for (int step = 0; step < steps; step++)
        {
            var valuesAtStep = new decimal[allCurves.Count];
            for (int i = 0; i < allCurves.Count; i++)
            {
                // Some curves may be shorter; clamp to their length
                valuesAtStep[i] = step < allCurves[i].Count ? allCurves[i][step] : allCurves[i][^1];
            }

            Array.Sort(valuesAtStep);
            p5.Add(Percentile(valuesAtStep, 0.05));
            p50.Add(Percentile(valuesAtStep, 0.50));
            p95.Add(Percentile(valuesAtStep, 0.95));
        }

        return (p5, p50, p95);
    }

    // Computes the p-th percentile of a sorted array (0.0 to 1.0).
    private static decimal Percentile(decimal[] sortedValues, double p)
    {
        if (sortedValues.Length == 0) return 0m;
        if (sortedValues.Length == 1) return sortedValues[0];

        var index = p * (sortedValues.Length - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper) return sortedValues[lower];

        var weight = (decimal)(index - lower);
        return sortedValues[lower] * (1m - weight) + sortedValues[upper] * weight;
    }

    private static double Percentile(double[] sortedValues, double p)
    {
        if (sortedValues.Length == 0) return 0.0;
        if (sortedValues.Length == 1) return sortedValues[0];

        var index = p * (sortedValues.Length - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper) return sortedValues[lower];

        var weight = index - lower;
        return sortedValues[lower] * (1.0 - weight) + sortedValues[upper] * weight;
    }
}