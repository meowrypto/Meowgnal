using System.Collections.Generic;
using System.Threading.Tasks;
using Meowgnal.Models;

namespace Meowgnal.DataProviders;

// Every exchange connector (Binance, Hyperliquid, ...) implements this.
// Adding a new exchange later means writing one new class, not touching
// the rest of the app.
public interface IDataProvider
{
    string Name { get; }
    Task<List<Bar>> GetHistoricalCandlesAsync(string symbol, string timeframe, int limit = 200);

    // Live last price + 24h change for several symbols at once (watchlist).
    Task<Dictionary<string, TickerInfo>> GetTickersAsync(IEnumerable<string> symbols);
}