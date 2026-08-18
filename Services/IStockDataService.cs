using AppleStockAPI.DTOs;

namespace AppleStockAPI.Services;

/// <summary>
/// Application-facing operations for Apple stock data. The controller depends on this
/// abstraction rather than on EF Core or HttpClient directly.
/// </summary>
public interface IStockDataService
{
    /// <summary>
    /// Fetch AAPL daily data from Alpha Vantage (the free compact feed — the latest 100 days)
    /// and store any new days.
    /// </summary>
    Task<IngestionResult> IngestAppleStockDataAsync(CancellationToken cancellationToken = default);

    /// <summary>Return a filtered, sorted, paged view of stored records (database-side).</summary>
    Task<PagedResult<StockPriceDto>> GetStocksAsync(
        StockQueryParameters query,
        CancellationToken cancellationToken = default);

    /// <summary>Return the most recent stored AAPL record, or null if none exist.</summary>
    Task<StockPriceDto?> GetLatestAppleStockDataAsync(CancellationToken cancellationToken = default);

    /// <summary>Return the AAPL record for a specific trading day, or null.</summary>
    Task<StockPriceDto?> GetStockByDateAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>Delete every stored record and return how many rows were removed.</summary>
    Task<int> ClearAllAsync(CancellationToken cancellationToken = default);
}
