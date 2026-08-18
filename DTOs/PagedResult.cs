namespace AppleStockAPI.DTOs;

/// <summary>
/// A single page of results plus the paging metadata the frontend needs to render controls.
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}
