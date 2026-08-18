namespace AppleStockAPI.DTOs;

/// <summary>
/// Query-string parameters for GET /api/stocks. Bound with [FromQuery].
/// All filtering, sorting and paging described here is applied in the database, not in memory.
/// </summary>
public class StockQueryParameters
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    /// <summary>1-based page number. Values below 1 are clamped to 1.</summary>
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Rows per page. Clamped to the 1..100 range.</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? DefaultPageSize : (value > MaxPageSize ? MaxPageSize : value);
    }

    /// <summary>Free-text term matched against Symbol, or interpreted as a date prefix (yyyy, yyyy-MM, yyyy-MM-dd).</summary>
    public string? Search { get; set; }

    /// <summary>Inclusive lower bound on the trading day.</summary>
    public DateOnly? FromDate { get; set; }

    /// <summary>Inclusive upper bound on the trading day.</summary>
    public DateOnly? ToDate { get; set; }

    /// <summary>One of: priceDate, close, volume. Anything else falls back to priceDate.</summary>
    public string SortBy { get; set; } = "priceDate";

    /// <summary>"asc" or "desc" (default).</summary>
    public string SortDirection { get; set; } = "desc";
}
