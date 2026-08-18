namespace AppleStockAPI.Options;

/// <summary>
/// Strongly typed Alpha Vantage settings bound from the "AlphaVantage" configuration section.
/// Nothing here is hard-coded in the service layer.
/// </summary>
public class AlphaVantageOptions
{
    public const string SectionName = "AlphaVantage";

    /// <summary>The value the committed appsettings.json ships with. Treated as "no key configured".</summary>
    public const string PlaceholderApiKey = "YOUR_API_KEY_HERE";

    public string BaseUrl { get; set; } = "https://www.alphavantage.co/query";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>True when no real key has been supplied (empty or still the placeholder).</summary>
    public bool HasValidApiKey =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.Equals(ApiKey, PlaceholderApiKey, StringComparison.OrdinalIgnoreCase);
}
