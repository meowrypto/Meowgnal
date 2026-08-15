using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Meowgnal.Models;
using Meowgnal.Services;

namespace Meowgnal.Views;

public sealed class IndicatorRowViewModel : INotifyPropertyChanged
{
    public IndicatorInfo Info { get; }

    private bool _isFavorite;
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite == value) return;
            _isFavorite = value;
            OnPropertyChanged(nameof(IsFavorite));
            OnPropertyChanged(nameof(FavIcon));
        }
    }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(ActiveIcon));
        }
    }

    public string FavIcon => IsFavorite ? "⭐" : "☆";
    public string ActiveIcon => IsActive ? "✓" : "";
    public string Title => Info.Label;
    public string Description => Info.Description;

    public IndicatorRowViewModel(IndicatorInfo info) => Info = info;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class IndicatorPanel : UserControl
{
    private readonly IndicatorSettingsFile _settings = IndicatorSettingsStorageService.Load();

    private readonly List<IndicatorRowViewModel> _all =
        IndicatorRegistry.All.Select(i => new IndicatorRowViewModel(i)).ToList();

    private readonly ObservableCollection<IndicatorRowViewModel> _favoriteRows = new();
    private readonly ObservableCollection<IndicatorRowViewModel> _technicalRows = new();

    public event Action<IndicatorInfo>? IndicatorSelected;

    public IndicatorPanel()
    {
        InitializeComponent();
        FavoritesList.ItemsSource = _favoriteRows;
        TechnicalList.ItemsSource = _technicalRows;

        foreach (var vm in _all)
            vm.IsFavorite = _settings.FavoriteIndicatorTypes.Contains(vm.Info.Type);

        ApplyFilter();
    }

    // Shows a check mark next to indicators currently on the chart.
    public void RefreshActiveTypes(IEnumerable<string> activeTypes)
    {
        var set = new HashSet<string>(activeTypes);
        foreach (var vm in _all)
            vm.IsActive = set.Contains((vm.Info.Type ?? "").ToLowerInvariant());
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = (SearchBox.Text ?? "").Trim().ToLowerInvariant();

        _technicalRows.Clear();
        foreach (var vm in _all.Where(v => Matches(v, q)))
            _technicalRows.Add(vm);

        _favoriteRows.Clear();
        foreach (var vm in _all.Where(v => v.IsFavorite && Matches(v, q)))
            _favoriteRows.Add(vm);

        FavoritesEmptyHint.Visibility = _favoriteRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool Matches(IndicatorRowViewModel vm, string q) =>
        string.IsNullOrEmpty(q)
        || vm.Info.Type.ToLowerInvariant().Contains(q)
        || vm.Info.Label.ToLowerInvariant().Contains(q)
        || vm.Info.Description.ToLowerInvariant().Contains(q);

    private void Star_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not IndicatorRowViewModel vm) return;

        vm.IsFavorite = !vm.IsFavorite;
        if (vm.IsFavorite) _settings.FavoriteIndicatorTypes.Add(vm.Info.Type);
        else _settings.FavoriteIndicatorTypes.Remove(vm.Info.Type);
        IndicatorSettingsStorageService.Save(_settings);

        ApplyFilter();
    }

    private void Row_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not IndicatorRowViewModel vm) return;
        IndicatorSelected?.Invoke(vm.Info);
    }

    #region Category collapse

    private void FavoritesHeader_Click(object sender, RoutedEventArgs e) =>
        ToggleSection(FavoritesBody, FavoritesArrow);

    private void TechnicalHeader_Click(object sender, RoutedEventArgs e) =>
        ToggleSection(TechnicalBody, TechnicalArrow);

    private void FundamentalHeader_Click(object sender, RoutedEventArgs e) =>
        ToggleSection(FundamentalBody, FundamentalArrow);

    private static void ToggleSection(UIElement body, TextBlock arrow)
    {
        var isOpen = body.Visibility == Visibility.Visible;
        body.Visibility = isOpen ? Visibility.Collapsed : Visibility.Visible;
        arrow.Text = isOpen ? "▸" : "▾";
    }

    #endregion
}