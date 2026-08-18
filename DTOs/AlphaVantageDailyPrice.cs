using System.Text.Json.Serialization;

namespace AppleStockAPI.DTOs;

/// <summary>
/// One day's values inside Alpha Vantage's "Time Series (Daily)" map.
/// Alpha Vantage sends every number as a string, so these are strings until we map them.
/// </summary>
public class AlphaVantageDailyPrice
{
    [JsonPropertyName("1. open")]
    public string Open { get; set; } = string.Empty;

    [JsonPropertyName("2. high")]
    public string High { get; set; } = string.Empty;

    [JsonPropertyName("3. low")]
    public string Low { get; set; } = string.Empty;

    [JsonPropertyName("4. close")]
    public string Close { get; set; } = string.Empty;

    [JsonPropertyName("5. volume")]
    public string Volume { get; set; } = string.Empty;
}
