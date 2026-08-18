namespace AppleStockAPI.DTOs;

/// <summary>
/// Summary of a single ingestion run, surfaced to the caller so the demo is easy to read.
/// </summary>
public class IngestionResult
{
    public string Symbol { get; set; } = "AAPL";

    public int RecordsReceived { get; set; }

    public int RecordsInserted { get; set; }

    public int RecordsSkipped { get; set; }
}
