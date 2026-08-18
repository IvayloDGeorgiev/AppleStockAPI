namespace AppleStockAPI.Options;

/// <summary>
/// Bound from the "Database" configuration section. A single Provider value selects the
/// EF Core provider rather than several competing boolean flags.
/// </summary>
public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Either "SqlServer" or "Sqlite".</summary>
    public string Provider { get; set; } = "SqlServer";
}
