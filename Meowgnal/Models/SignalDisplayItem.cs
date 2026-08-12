namespace Meowgnal.Models;

// A lightweight view-model for showing one signal in the dashboard's signal panel.
public sealed class SignalDisplayItem
{
    public string Symbol { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = ""; // "buy" | "sell"
    public string Time { get; set; } = "";

    // Phase 26 — Signal Quality Score (0-100)
    public int QualityScore { get; set; }
    public string QualityLabel { get; set; } = ""; // e.g. "A+", "B", "C"
    public string QualityReason { get; set; } = ""; // short explanation
}