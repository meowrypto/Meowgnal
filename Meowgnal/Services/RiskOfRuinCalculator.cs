using System;

namespace Meowgnal.Services;

// Computes the probability of losing the entire trading account using the
// classic Gambler's Ruin formula (equal-size bets approximation).
// Returns a value between 0 (no risk) and 1 (certain ruin).
//
// After the Monte Carlo engine is built (next task), this tool can be wired
// to it for a more accurate estimate that handles asymmetric win/loss sizes.
public static class RiskOfRuinCalculator
{
    public static double Calculate(double winRateFraction, double riskPerTradeFraction)
    {
        if (winRateFraction <= 0 || winRateFraction >= 1) return 1.0;
        if (riskPerTradeFraction <= 0 || riskPerTradeFraction >= 1) return 1.0;

        var edge = (2 * winRateFraction) - 1;

        // No statistical edge → ruin is certain in the long run.
        if (edge <= 0) return 1.0;

        var units = 1.0 / riskPerTradeFraction;
        var ror = Math.Pow((1 - edge) / (1 + edge), units);

        return Math.Clamp(ror, 0, 1);
    }
}