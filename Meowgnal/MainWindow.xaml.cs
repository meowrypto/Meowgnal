using System.Windows;
using Meowgnal.DataProviders;

namespace Meowgnal;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        var provider = new BinanceDataProvider();
        var bars = await provider.GetHistoricalCandlesAsync("BTC/USDT", "1h", limit: 5);

        ResultText.Text = $"Got {bars.Count} candles.\nLast close: {bars[^1].Close}";
    }
}