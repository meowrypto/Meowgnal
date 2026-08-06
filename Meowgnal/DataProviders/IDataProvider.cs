using System.Collections.Generic;
using System.Threading.Tasks;
using Meowgnal.Models;

namespace Meowgnal.DataProviders;

// Every exchange connector (Binance, Hyperliquid, ...) implements this.
// This is the "Data Provider" abstraction we planned from the start —
// adding a new exchange later means writing one new class, not touching
// the rest of the app.
public interface IDataProvider
{
    string Name { get; }

    Task<List<Bar>> GetHistoricalCandlesAsync(string symbol, string timeframe, int limit = 200);
}