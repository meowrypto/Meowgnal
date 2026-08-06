using System.Linq;
using System.Windows;
using Meowgnal.DataProviders;
using Meowgnal.Engine;
using Meowgnal.Models;

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
        ResultText.Text = $"[Binance] Got {bars.Count} candles.\nLast close: {bars[^1].Close}";
    }

    private async void TestHyperliquidButton_Click(object sender, RoutedEventArgs e)
    {
        var provider = new HyperliquidDataProvider();
        var bars = await provider.GetHistoricalCandlesAsync("BTC/USDT", "1h", limit: 5);
        ResultText.Text = $"[Hyperliquid] Got {bars.Count} candles.\nLast close: {bars[^1].Close}";
    }

    private async void TestIndicatorButton_Click(object sender, RoutedEventArgs e)
    {
        var provider = new BinanceDataProvider();
        // EMA9 needs a real warmup window, so grab plenty of candles.
        var bars = await provider.GetHistoricalCandlesAsync("BTC/USDT", "1h", limit: 100);

        var indicatorDef = new IndicatorDefinition
        {
            Id = "ema9",
            Type = "EMA",
            Params = new() { ["period"] = 9 }
        };

        var emaValues = IndicatorEngine.Calculate(bars, indicatorDef);
        var lastEma = emaValues.Last();

        ResultText.Text = $"[EMA9] Last close: {bars[^1].Close}\nEMA9: {lastEma:N2}";
    }
}