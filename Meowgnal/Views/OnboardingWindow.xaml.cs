using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Meowgnal.Views;

public partial class OnboardingWindow : Window
{
    public string ChosenName { get; private set; } = "";
    public string ChosenAvatar { get; private set; } = "🐱";
    public bool ChoseGuest { get; private set; } = true;

    public OnboardingWindow()
    {
        InitializeComponent();
    }

    #region Custom title bar

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // No double-click to maximize on this modal dialog
        DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    #endregion

    private void CreateProfile_Click(object sender, RoutedEventArgs e)
    {
        ChosenName = string.IsNullOrWhiteSpace(NameBox.Text) ? "Trader" : NameBox.Text.Trim();
        ChosenAvatar = (AvatarCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "🐱";
        ChoseGuest = false;
        DialogResult = true;
        Close();
    }

    private void Guest_Click(object sender, RoutedEventArgs e)
    {
        ChoseGuest = true;
        ChosenName = "Guest";
        ChosenAvatar = "🐱";
        DialogResult = true;
        Close();
    }
}