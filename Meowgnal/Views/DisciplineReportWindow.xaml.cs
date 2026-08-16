using System.Windows;
using System.Windows.Input;
using Meowgnal.Services;

namespace Meowgnal.Views;

public partial class DisciplineReportWindow : Window
{
    public DisciplineReportWindow()
    {
        InitializeComponent();
        RenderReport(ChecklistAnalyticsService.Analyze(JournalStorageService.Load()));
    }

    #region Custom title bar

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            var point = PointToScreen(e.GetPosition(this));
            WindowState = WindowState.Normal;
            Left = point.X - Width / 2;
            Top = point.Y - 15;
        }
        DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    #endregion

    private void RenderReport(ChecklistAnalyticsService.DisciplineReport r)
    {
        if (!r.HasEnoughData)
        {
            NoDataBanner.Visibility = Visibility.Visible;
            PatternPanel.Visibility = Visibility.Collapsed;
            CompletedWinRateText.Text = "—";
            SkippedWinRateText.Text = "—";
            CompletedCountText.Text = $"{r.CompletedCount} completed trades";
            SkippedCountText.Text = $"{r.SkippedOrCriticalCount} skipped or flagged trades";
            SummaryText.Text = r.Summary;
            return;
        }

        CompletedWinRateText.Text = $"{r.CompletedWinRate:N0}%";
        SkippedWinRateText.Text = $"{r.SkippedWinRate:N0}%";
        CompletedCountText.Text = $"{r.CompletedCount} trades";
        SkippedCountText.Text = $"{r.SkippedOrCriticalCount} trades";

        var showPattern = r.HasTimePattern || r.HasPostLossPattern;
        PatternPanel.Visibility = showPattern ? Visibility.Visible : Visibility.Collapsed;

        if (r.HasTimePattern)
        {
            TimePatternText.Visibility = Visibility.Visible;
            TimePatternText.Text = "🌙 " + r.TimePatternDescription;
        }
        else TimePatternText.Visibility = Visibility.Collapsed;

        if (r.HasPostLossPattern)
        {
            PostLossPatternText.Visibility = Visibility.Visible;
            PostLossPatternText.Text = "🔥 " + r.PostLossDescription;
        }
        else PostLossPatternText.Visibility = Visibility.Collapsed;

        SummaryText.Text = r.Summary;
    }
}