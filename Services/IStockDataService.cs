using AppleStockAPI.DTOs;

namespace AppleStockAPI.Services;

/// <summary>
/// Application-facing operations for Apple stock data. The controller depends on this
/// abstraction rather than on EF Core or HttpClient directly.
/// </summary>
public interface IStockDataService
{
    /// <summary>
    /// Fetch AAPL daily data from Alpha Vantage and store any new days. <paramref name="recordCount"/>
    /// is how many of the most recent trading days to pull (Alpha Vantage is queried with
    /// outputsize=compact for up to 100, otherwise outputsize=full).
    /// </summary>
    Task<IngestionResult> IngestAppleStockDataAsync(int recordCount, CancellationToken cancellationToken = default);

    /// <summary>Return a filtered, sorted, paged view of stored records (database-side).</summary>
    Task<PagedResult<StockPriceDto>> GetStocksAsync(
        StockQueryParameters query,
        CancellationToken cancellationToken = default);

    /// <summary>Return the most recent stored AAPL record, or null if none exist.</summary>
    Task<StockPriceDto?> GetLatestAppleStockDataAsync(CancellationToken cancellationToken = default);

    /// <summary>Return the AAPL record for a specific trading day, or null.</summary>
    Task<StockPriceDto?> GetStockByDateAsync(DateOnly date, CancellationToken cancellationToken = default);
}
