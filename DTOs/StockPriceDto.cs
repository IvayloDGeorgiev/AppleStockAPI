namespace AppleStockAPI.DTOs;

/// <summary>
/// The shape returned to API consumers. Deliberately excludes internal columns (Id,
/// IngestedAtUtc) and exposes the trading day as a date-only value that serialises as
/// "2026-08-18".
/// </summary>
public class StockPriceDto
{
    public string Symbol { get; set; } = string.Empty;

    public DateOnly PriceDate { get; set; }

    public decimal Open { get; set; }

    public decimal High { get; set; }

    public decimal Low { get; set; }

    public decimal Close { get; set; }

    public long Volume { get; set; }

    public string Source { get; set; } = string.Empty;
}
