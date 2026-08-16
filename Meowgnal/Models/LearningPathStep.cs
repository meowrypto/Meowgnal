namespace Meowgnal.Models;

// One step in the guided learning path.
public sealed class LearningPathStep
{
    public int StepNumber { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}

// The fixed 4-step sequence every new user follows.
public static class LearningPathSteps
{
    public static readonly LearningPathStep[] All =
    {
        new()
        {
            StepNumber = 1,
            Title = "Try a ready-made template",
            Description = "Pick a template from the store and add it to your chart. No setup needed — just click \"Use template\"."
        },
        new()
        {
            StepNumber = 2,
            Title = "See why it worked",
            Description = "We'll run a quick backtest on the template you just added and explain the results in plain English, so you understand what the strategy actually does."
        },
        new()
        {
            StepNumber = 3,
            Title = "Tweak one parameter",
            Description = "Open the Strategy Builder, change one indicator period (like RSI from 14 to 10), and hit \"Test this strategy\" again. See how a small change affects the results."
        },
        new()
        {
            StepNumber = 4,
            Title = "Build one from scratch",
            Description = "Open an empty Strategy Builder and create your own strategy from zero. You now know enough to do it!"
        },
    };
}