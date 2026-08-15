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
    // Favorites live in AppSettings (shared, persistent).
    private readonly List<string> _favoriteIds = SettingsStorageService.Load().FavoriteIndicatorIds;

    private readonly List<IndicatorRowViewModel> _all =
        IndicatorRegistry.All.Select(i => new IndicatorRowViewModel(i)).ToList();

    private readonly ObservableCollection<IndicatorRowViewModel> _favoriteRows = new();
    private readonly ObservableCollection<IndicatorRowViewModel> _movingAvgRows = new();
    private readonly ObservableCollection<IndicatorRowViewModel> _oscillatorRows = new();
    private readonly ObservableCollection<IndicatorRowViewModel> _volatilityRows = new();
    private readonly ObservableCollection<IndicatorRowViewModel> _volumeRows = new();
    private readonly ObservableCollection<IndicatorRowViewModel> _trendRows = new();
    private readonly ObservableCollection<IndicatorRowViewModel> _fundamentalRows = new();

    public event Action<IndicatorInfo>? IndicatorSelected;

    public IndicatorPanel()
    {
        InitializeComponent();
        FavoritesList.ItemsSource = _favoriteRows;
        MovingAvgList.ItemsSource = _movingAvgRows;
        OscillatorList.ItemsSource = _oscillatorRows;
        VolatilityList.ItemsSource = _volatilityRows;
        VolumeList.ItemsSource = _volumeRows;
        TrendList.ItemsSource = _trendRows;
        FundamentalList.ItemsSource = _fundamentalRows;

        foreach (var id in _favoriteIds)
        {
            var vm = _all.FirstOrDefault(v => v.Info.Type == id);
            if (vm is not null) vm.IsFavorite = true;
        }

        ApplyFilter();
    }

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

        _movingAvgRows.Clear();
        _oscillatorRows.Clear();
        _volatilityRows.Clear();
        _volumeRows.Clear();
        _trendRows.Clear();
        _fundamentalRows.Clear();

        foreach (var vm in _all.Where(v => Matches(v, q)))
        {
            switch (vm.Info.SubCategory)
            {
                case "Moving Averages": _movingAvgRows.Add(vm); break;
                case "Oscillators": _oscillatorRows.Add(vm); break;
                case "Volatility": _volatilityRows.Add(vm); break;
                case "Volume": _volumeRows.Add(vm); break;
                case "Trend": _trendRows.Add(vm); break;
                case "Fundamental": _fundamentalRows.Add(vm); break;
            }
        }

        // Favorites keep the order the user added them (not alphabetical).
        _favoriteRows.Clear();
        foreach (var id in _favoriteIds)
        {
            var vm = _all.FirstOrDefault(v => v.Info.Type == id);
            if (vm is not null && Matches(vm, q)) _favoriteRows.Add(vm);
        }

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
        if (vm.IsFavorite) _favoriteIds.Add(vm.Info.Type);
        else _favoriteIds.Remove(vm.Info.Type);

        var settings = SettingsStorageService.Load();
        settings.FavoriteIndicatorIds = _favoriteIds;
        SettingsStorageService.Save(settings);

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

    private void MovingAvgHeader_Click(object sender, RoutedEventArgs e) =>
        ToggleSection(MovingAvgBody, MovingAvgArrow);

    private void OscillatorHeader_Click(object sender, RoutedEventArgs e) =>
        ToggleSection(OscillatorBody, OscillatorArrow);

    private void VolatilityHeader_Click(object sender, RoutedEventArgs e) =>
        ToggleSection(VolatilityBody, VolatilityArrow);

    private void VolumeHeader_Click(object sender, RoutedEventArgs e) =>
        ToggleSection(VolumeBody, VolumeArrow);

    private void TrendHeader_Click(object sender, RoutedEventArgs e) =>
        ToggleSection(TrendBody, TrendArrow);

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