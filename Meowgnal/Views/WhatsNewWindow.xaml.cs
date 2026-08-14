using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Meowgnal.Views;

/// <summary>Shows the list of new features for the latest versions.</summary>
public partial class WhatsNewWindow : Window
{
    public WhatsNewWindow()
    {
        InitializeComponent();
        BuildContent();
    }

    #region Custom title bar

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // No double-click to maximize on this modal dialog (ResizeMode=NoResize)
        DragMove();
    }

    #endregion

    private void BuildContent()
    {
        AddHeader("Latest update - Drawing tools pro");
        AddItem("Multi-select drawings with Ctrl+Click; Delete removes all selected");
        AddItem("Drag the body of any drawing to move the whole shape");
        AddItem("Group drawings: Ctrl+G to group, Ctrl+Shift+G to ungroup");
        AddItem("Resize rectangles and ellipses with 4 corner/edge handles");
        AddItem("Price alerts on all horizontal drawings with a bell icon");
        AddItem("Snap new drawing points to the nearest candle");
        AddItem("Layering: right-click a drawing for Bring to Front / Send to Back");
        AddItem("Smoother Brush and Highlighter strokes");
        AddItem("Fibonacci extension levels (127.2%, 161.8%, 261.8%)");
        AddItem("Hover tooltip shows drawing info");
        AddItem("Text, Note and Sticker keep their text and support custom fonts");
        AddItem("Arc curvature can be edited after drawing");

        AddHeader("Earlier - Core platform");
        AddItem("Custom title bar, profile and onboarding, splash screen");
        AddItem("License management with demo period");
        AddItem("Dark / Light / System / Custom themes");
        AddItem("Watchlist, Signals and Paper Trading panels");
        AddItem("Strategy builder, backtest and journal");
        AddItem("Undo/Redo, copy/paste and object list for drawings");
    }

    private void AddHeader(string text)
    {
        ContentPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Accent"),
            Margin = new Thickness(0, 10, 0, 6)
        });
    }

    private void AddItem(string text)
    {
        ContentPanel.Children.Add(new TextBlock
        {
            Text = "- " + text,
            Foreground = (Brush)FindResource("TextSecondary"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 0, 4)
        });
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}