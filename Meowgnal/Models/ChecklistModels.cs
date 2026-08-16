using System;
using System.Collections.Generic;

namespace Meowgnal.Models;

// One question in the pre-trade discipline checklist.
public sealed class ChecklistItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Question { get; set; } = "";
    // If true and the user answers "Yes", a soft warning is shown.
    public bool IsCritical { get; set; }
}

// The user's yes/no answer to one question, stored alongside the trade.
public sealed class ChecklistAnswer
{
    public string QuestionId { get; set; } = "";
    public string QuestionText { get; set; } = "";
    public bool IsCritical { get; set; }
    // true = "Yes" (which is a red flag for critical items)
    public bool Answer { get; set; }
}

// Result of a full checklist run, attached to the resulting PaperTrade / JournalEntry.
public sealed class ChecklistResult
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<ChecklistAnswer> Answers { get; set; } = new();
    public bool Skipped { get; set; }

    // True only when the user answered every question AND no critical item was "Yes".
    public bool FullyCompleted
    {
        get
        {
            if (Skipped) return false;
            foreach (var a in Answers)
                if (a.IsCritical && a.Answer) return false;
            return true;
        }
    }
}

// Factory for the 6 default discipline questions.
public static class DefaultChecklists
{
    public static List<ChecklistItem> GetDefault() => new()
    {
        new() { Id = "rule_follow", Question = "Am I following my strategy's exact rules, not a gut feeling?", IsCritical = false },
        new() { Id = "size_calc", Question = "Did I calculate position size based on my risk plan?", IsCritical = false },
        new() { Id = "sl_set", Question = "Did I set my stop loss BEFORE entering?", IsCritical = false },
        new() { Id = "revenge", Question = "Am I trying to win back a recent loss right now?", IsCritical = true },
        new() { Id = "emotion", Question = "Am I tired, angry, or stressed right now?", IsCritical = true },
        new() { Id = "risk_limit", Question = "Does this trade risk more than my normal per-trade limit?", IsCritical = true },
    };
}