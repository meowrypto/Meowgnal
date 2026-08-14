using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace Meowgnal.Views;

public sealed class ConditionNodeViewModel : INotifyPropertyChanged
{
    public bool IsGroup { get; }
    public bool IsLeaf => !IsGroup;
    public int Depth { get; set; }
    public ConditionNodeViewModel? Parent { get; set; }

    public ObservableCollection<ConditionNodeViewModel> Children { get; } = new();
    private string _mode = "all";
    public string Mode
    {
        get => _mode;
        set { _mode = value; OnPropertyChanged(nameof(Mode)); }
    }
    public double? MinScore { get; set; }

    public string Left { get; set; } = "price";
    public string Op { get; set; } = "crossesAbove";
    public string Right { get; set; } = StrategyBuilderWindow.NumberToken;
    public string Number { get; set; } = "30";
    public double Weight { get; set; } = 1;
    public double Tolerance { get; set; } = 0.5;

    public Visibility GroupVisibility => IsGroup ? Visibility.Visible : Visibility.Collapsed;
    public Visibility LeafVisibility => IsLeaf ? Visibility.Visible : Visibility.Collapsed;
    public bool CanAddGroup => Depth < 3;
    public bool CanRemove => Parent is not null;
    public Visibility RemoveVisibility => CanRemove ? Visibility.Visible : Visibility.Collapsed;

    public Thickness Margin => new(Depth * 16, 0, 0, 8);

    public ConditionNodeViewModel(bool isGroup)
    {
        IsGroup = isGroup;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}