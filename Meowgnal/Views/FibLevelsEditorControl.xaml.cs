using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Meowgnal.Models;

namespace Meowgnal.Views
{
    public partial class FibLevelsEditorControl : UserControl
    {
        public static readonly DependencyProperty LevelsProperty =
            DependencyProperty.Register(
                nameof(Levels),
                typeof(ObservableCollection<FibLevel>),
                typeof(FibLevelsEditorControl),
                new FrameworkPropertyMetadata(
                    new ObservableCollection<FibLevel>(),
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnLevelsChanged));

        public ObservableCollection<FibLevel> Levels
        {
            get => (ObservableCollection<FibLevel>)GetValue(LevelsProperty);
            set => SetValue(LevelsProperty, value);
        }

        public FibLevelsEditorControl()
        {
            InitializeComponent();
            LevelsList.ItemsSource = Levels;
        }

        private static void OnLevelsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (FibLevelsEditorControl)d;
            control.LevelsList.ItemsSource = e.NewValue as ObservableCollection<FibLevel>;
        }

        private void AddLevel_Click(object sender, RoutedEventArgs e)
        {
            if (Levels != null)
            {
                Levels.Add(new FibLevel
                {
                    Ratio = 0.5,
                    Enabled = true,
                    Color = "#2962FF",
                    Label = "0.500"
                });
            }
        }

        private void RemoveLevel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is FibLevel level && Levels != null)
            {
                Levels.Remove(level);
            }
        }
    }
}