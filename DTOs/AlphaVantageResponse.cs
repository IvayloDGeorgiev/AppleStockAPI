using System.Text.Json.Serialization;

namespace AppleStockAPI.DTOs;

/// <summary>
/// Maps the raw JSON returned by Alpha Vantage's TIME_SERIES_DAILY endpoint.
/// Alpha Vantage also uses the "Note"/"Information"/"Error Message" fields to report
/// rate limits and errors, so we capture those to produce meaningful messages.
/// </summary>
public class AlphaVantageResponse
{
    [JsonPropertyName("Time Series (Daily)")]
    public Dictionary<string, AlphaVantageDailyPrice>? TimeSeries { get; set; }

    [JsonPropertyName("Error Message")]
    public string? ErrorMessage { get; set; }

    /// <summary>Present when the free-tier request/day limit is hit.</summary>
    [JsonPropertyName("Note")]
    public string? Note { get; set; }

    /// <summary>Present for informational responses (e.g. invalid API key, throttling).</summary>
    [JsonPropertyName("Information")]
    public string? Information { get; set; }
}
