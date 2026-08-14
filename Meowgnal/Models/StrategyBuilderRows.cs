using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Meowgnal.Models;

// Lightweight row objects the Strategy Builder UI binds to directly.
// Implements INotifyPropertyChanged so code-side changes (e.g. applying the
// default period when the indicator type changes) refresh the UI instantly.
public sealed class IndicatorRow : INotifyPropertyChanged
{
    private string _id = "";
    private string _type = "EMA";
    private int _period = 14;

    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public string Type
    {
        get => _type;
        set { _type = value; OnPropertyChanged(); }
    }

    public int Period
    {
        get => _period;
        set { _period = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class ConditionRow
{
    public string Left { get; set; } = "";
    public string Op { get; set; } = "crossesAbove";
    public string Right { get; set; } = "";
    public double Weight { get; set; } = 1;
    public double Tolerance { get; set; } = 0.5;
}