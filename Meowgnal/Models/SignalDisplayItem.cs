namespace Meowgnal.Models;

// A lightweight view-model for showing one signal in the dashboard's signal panel.
public sealed class SignalDisplayItem
{
    public string Symbol { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = ""; // "buy" | "sell"
    public string Time { get; set; } = "";
}