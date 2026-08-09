namespace Meowgnal.Models;

// One symbol row inside a watchlist, remembering which exchange it came from.
public sealed class WatchlistItem
{
    public string Symbol { get; set; } = "";
    public string DataSource { get; set; } = "binance"; // binance | hyperliquid
}

// A named watchlist (users can create several, like TradingView).
public sealed class WatchlistDefinition
{
    public string Name { get; set; } = "Main";
    public List<WatchlistItem> Items { get; set; } = new();
}

// The whole encrypted watchlists file: all lists + which one is active.
public sealed class WatchlistsFile
{
    public string ActiveListName { get; set; } = "Main";
    public List<WatchlistDefinition> Lists { get; set; } = new();
}