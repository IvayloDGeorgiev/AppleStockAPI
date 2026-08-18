namespace AppleStockAPI.Models;

/// <summary>
/// A single day's price record for one stock symbol.
/// This is the entity that Entity Framework Core maps to the StockPrices table.
/// </summary>
public class StockPrice
{
    /// <summary>Internal surrogate primary key (database identity).</summary>
    public int Id { get; set; }

    /// <summary>Ticker symbol, e.g. "AAPL". Kept generic so other symbols can be stored later.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>The trading day this record represents (date component only).</summary>
    public DateTime PriceDate { get; set; }

    /// <summary>Opening share price.</summary>
    public decimal Open { get; set; }

    /// <summary>Highest price during the trading day.</summary>
    public decimal High { get; set; }

    /// <summary>Lowest price during the trading day.</summary>
    public decimal Low { get; set; }

    /// <summary>Closing share price.</summary>
    public decimal Close { get; set; }

    /// <summary>Number of shares traded.</summary>
    public long Volume { get; set; }

    /// <summary>Where the data originated (e.g. "Alpha Vantage").</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>When our application imported the record (UTC). Distinct from PriceDate.</summary>
    public DateTime IngestedAtUtc { get; set; }
}
