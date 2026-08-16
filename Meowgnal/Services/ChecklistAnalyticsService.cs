using System;
using System.Collections.Generic;
using System.Linq;
using Meowgnal.Models;

namespace Meowgnal.Services;

// Analyzes the journal to find discipline patterns: how checklists
// correlate with win rate, time of day, and trades after losses.
public static class ChecklistAnalyticsService
{
    // The analysis result shown in the Discipline Report window.
    public sealed class DisciplineReport
    {
        public bool HasEnoughData { get; set; }
        public int CompletedCount { get; set; }
        public int SkippedOrCriticalCount { get; set; }
        public double CompletedWinRate { get; set; }
        public double SkippedWinRate { get; set; }

        public bool HasTimePattern { get; set; }
        public string TimePatternDescription { get; set; } = "";

        public bool HasPostLossPattern { get; set; }
        public string PostLossDescription { get; set; } = "";

        public string Summary { get; set; } = "";
    }

    // Minimum trades per group before we trust the numbers.
    private const int MinTradesPerGroup = 10;

    public static DisciplineReport Analyze(JournalFile journal)
    {
        var result = new DisciplineReport();

        if (journal is null || journal.Entries.Count < 5)
            return result;

        var completed = new List<JournalEntry>();
        var skippedOrCritical = new List<JournalEntry>();

        foreach (var e in journal.Entries)
        {
            if (e.ChecklistResult is null) continue;
            if (e.ChecklistResult.Skipped) skippedOrCritical.Add(e);
            else if (!e.ChecklistResult.FullyCompleted) skippedOrCritical.Add(e);
            else completed.Add(e);
        }

        result.CompletedCount = completed.Count;
        result.SkippedOrCriticalCount = skippedOrCritical.Count;

        // Need enough trades in both buckets to compare.
        if (completed.Count < MinTradesPerGroup || skippedOrCritical.Count < MinTradesPerGroup)
        {
            result.HasEnoughData = false;
            result.Summary = "Not enough data yet — keep trading to unlock this report.";
            return result;
        }

        result.HasEnoughData = true;
        result.CompletedWinRate = WinRate(completed);
        result.SkippedWinRate = WinRate(skippedOrCritical);

        // Time-of-day pattern: do skips cluster at certain hours?
        AnalyzeTimePattern(journal.Entries, completed, skippedOrCritical, result);

        // Post-loss pattern: are skips more common right after losing trades?
        AnalyzePostLossPattern(journal.Entries, result);

        result.Summary = BuildSummary(result);
        return result;
    }

    private static double WinRate(List<JournalEntry> entries)
    {
        if (entries.Count == 0) return 0;
        var wins = entries.Count(e => e.PnL > 0);
        return (double)wins / entries.Count * 100.0;
    }

    private static void AnalyzeTimePattern(
        IEnumerable<JournalEntry> all,
        List<JournalEntry> completed,
        List<JournalEntry> skipped,
        DisciplineReport result)
    {
        if (completed.Count < 5 || skipped.Count < 5) return;

        // Split the day into two buckets: "day" (6-22) and "late" (22-6).
        int completedLate = 0, skippedLate = 0;
        foreach (var e in completed) if (IsLateNight(e.OpenTime)) completedLate++;
        foreach (var e in skipped) if (IsLateNight(e.OpenTime)) skippedLate++;

        var completedLatePct = completedLate / (double)completed.Count * 100.0;
        var skippedLatePct = skippedLate / (double)skipped.Count * 100.0;

        // Only report a pattern if late-night skips are at least 2x the baseline.
        if (skippedLatePct > completedLatePct * 1.8 && skippedLate >= 3)
        {
            result.HasTimePattern = true;
            result.TimePatternDescription =
                $"You're about {SkippedLateMultiplier(skippedLatePct, completedLatePct):F1}x more likely to skip the checklist late at night (after 10 PM).";
        }
    }

    private static double SkippedLateMultiplier(double skippedPct, double completedPct)
    {
        if (completedPct <= 0.0001) return skippedPct > 0 ? 10 : 1;
        return skippedPct / completedPct;
    }

    private static bool IsLateNight(DateTime time)
    {
        var h = time.Hour;
        return h >= 22 || h < 6;
    }

    private static void AnalyzePostLossPattern(List<JournalEntry> all, DisciplineReport result)
    {
        if (all.Count < 5) return;

        var ordered = all.OrderBy(e => e.CloseTime).ToList();
        int normalSkipRate = 0, normalTotal = 0;
        int postLossSkipRate = 0, postLossTotal = 0;

        for (var i = 1; i < ordered.Count; i++)
        {
            var curr = ordered[i];
            var prev = ordered[i - 1];
            if (curr.ChecklistResult is null) continue;

            if (prev.PnL < 0)
            {
                postLossTotal++;
                if (curr.ChecklistResult.Skipped || !curr.ChecklistResult.FullyCompleted)
                    postLossSkipRate++;
            }
            else
            {
                normalTotal++;
                if (curr.ChecklistResult.Skipped || !curr.ChecklistResult.FullyCompleted)
                    normalSkipRate++;
            }
        }

        if (normalTotal < 3 || postLossTotal < 3) return;

        var normalPct = (double)normalSkipRate / normalTotal * 100.0;
        var postLossPct = (double)postLossSkipRate / postLossTotal * 100.0;

        if (postLossPct > normalPct * 1.5 && postLossSkipRate >= 2)
        {
            result.HasPostLossPattern = true;
            result.PostLossDescription =
                $"You skip the checklist {postLossPct:F0}% of the time right after a loss, vs {normalPct:F0}% normally — classic revenge-trading pressure.";
        }
    }

    private static string BuildSummary(DisciplineReport r)
    {
        var parts = new List<string>();

        if (r.CompletedWinRate > r.SkippedWinRate + 5)
            parts.Add($"Your win rate is {r.CompletedWinRate:N0}% when you complete the checklist, but only {r.SkippedWinRate:N0}% when you skip it.");
        else if (Math.Abs(r.CompletedWinRate - r.SkippedWinRate) <= 5)
            parts.Add($"Your win rate is similar whether you complete the checklist ({r.CompletedWinRate:N0}%) or skip it ({r.SkippedWinRate:N0}%).");
        else
            parts.Add($"Interestingly, skipping the checklist has a slightly better win rate ({r.SkippedWinRate:N0}% vs {r.CompletedWinRate:N0}%) — but the sample may be small.");

        if (r.HasTimePattern) parts.Add(r.TimePatternDescription);
        if (r.HasPostLossPattern) parts.Add(r.PostLossDescription);

        return string.Join(" ", parts);
    }
}