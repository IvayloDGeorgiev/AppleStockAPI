using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AppleStockAPI.Data;
using AppleStockAPI.DTOs;
using AppleStockAPI.Models;
using AppleStockAPI.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppleStockAPI.Services;

/// <summary>
/// Handles ingestion from Alpha Vantage and retrieval from the database. All querying
/// (filter, sort, paging) is composed as IQueryable and executed in the database.
/// </summary>
public class StockDataService : IStockDataService
{
    private const string Symbol = "AAPL";
    private const string SourceName = "Alpha Vantage";

    // Alpha Vantage's compact response returns the latest 100 days; beyond that we need "full".
    private const int CompactMaxDays = 100;

    private static readonly Regex YearOnly = new(@"^\d{4}$", RegexOptions.Compiled);
    private static readonly Regex YearMonth = new(@"^\d{4}-\d{2}$", RegexOptions.Compiled);
    private static readonly Regex YearMonthDay = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly StockDbContext _dbContext;
    private readonly AlphaVantageOptions _alphaVantage;
    private readonly ILogger<StockDataService> _logger;

    public StockDataService(
        HttpClient httpClient,
        StockDbContext dbContext,
        IOptions<AlphaVantageOptions> alphaVantageOptions,
        ILogger<StockDataService> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _alphaVantage = alphaVantageOptions.Value;
        _logger = logger;
    }

    public async Task<IngestionResult> IngestAppleStockDataAsync(int recordCount, CancellationToken cancellationToken = default)
    {
        if (recordCount < 1)
        {
            recordCount = CompactMaxDays;
        }

        // Use Alpha Vantage's small compact feed (last 100 days) for up to 100 records, and only
        // reach for the large "full" (20+ year, ~6,500 day) feed when more than 100 are requested.
        var useFullHistory = recordCount > CompactMaxDays;

        _logger.LogInformation(
            "Starting {Symbol} ingestion for the {Count} most recent days ({Feed} feed)",
            Symbol, recordCount, useFullHistory ? "full" : "compact");

        if (!_alphaVantage.HasValidApiKey)
        {
            // Meaningful, non-leaking error. The key itself is never logged or returned.
            throw new InvalidOperationException(
                "Alpha Vantage API key is not configured. Set AlphaVantage:ApiKey in " +
                "appsettings.Development.json (or the AlphaVantage__ApiKey environment variable).");
        }

        var requestUri = BuildRequestUri(useFullHistory);

        _logger.LogInformation("Calling Alpha Vantage for {Symbol}", Symbol);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(requestUri, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "External API request failed for {Symbol}", Symbol);
            throw new InvalidOperationException("Failed to reach Alpha Vantage. Please try again later.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("External API request failed for {Symbol} with status {StatusCode}",
                Symbol, (int)response.StatusCode);
            throw new InvalidOperationException(
                $"Alpha Vantage returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = DeserializeAndValidate(payload);

        var timeSeries = parsed.TimeSeries!;

        // Keep only the most recent `recordCount` days (keys are "yyyy-MM-dd", which sort
        // chronologically, so descending order gives us the newest days first).
        var selected = timeSeries
            .OrderByDescending(kvp => kvp.Key)
            .Take(recordCount)
            .ToList();

        _logger.LogInformation(
            "Received {Available} records for {Symbol}; taking the most recent {Taken}",
            timeSeries.Count, Symbol, selected.Count);

        // Existing days for this symbol, so we can skip anything already stored.
        var existingDates = await _dbContext.StockPrices
            .Where(s => s.Symbol == Symbol)
            .Select(s => s.PriceDate)
            .ToListAsync(cancellationToken);

        var existing = new HashSet<DateTime>(existingDates);
        var ingestedAt = DateTime.UtcNow;

        var toInsert = new List<StockPrice>();
        var skipped = 0;

        foreach (var (dateText, daily) in selected)
        {
            if (!TryMap(dateText, daily, ingestedAt, out var stockPrice))
            {
                // Malformed row from the API: skip rather than store invalid data.
                skipped++;
                continue;
            }

            // Skip if already in the database, or already queued in this batch.
            if (!existing.Add(stockPrice.PriceDate))
            {
                skipped++;
                continue;
            }

            toInsert.Add(stockPrice);
        }

        if (toInsert.Count > 0)
        {
            await _dbContext.StockPrices.AddRangeAsync(toInsert, cancellationToken);
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database operation failed while inserting {Symbol} records", Symbol);
                throw new InvalidOperationException("Failed to persist stock data.", ex);
            }
        }

        _logger.LogInformation(
            "Completed ingestion for {Symbol}: inserted {Inserted}, skipped {Skipped} duplicate records",
            Symbol, toInsert.Count, skipped);

        return new IngestionResult
        {
            Symbol = Symbol,
            RecordsReceived = selected.Count,
            RecordsInserted = toInsert.Count,
            RecordsSkipped = skipped
        };
    }

    public async Task<PagedResult<StockPriceDto>> GetStocksAsync(
        StockQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<StockPrice> q = _dbContext.StockPrices.AsNoTracking();

        q = ApplySearch(q, query.Search);
        q = ApplyDateRange(q, query.FromDate, query.ToDate);
        q = ApplySort(q, query.SortBy, query.SortDirection);

        // Count the filtered set, then page it — the database only returns one page.
        var totalCount = await q.CountAsync(cancellationToken);

        var pageEntities = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = pageEntities.Select(ToDto).ToList();

        var totalPages = query.PageSize == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)query.PageSize);

        return new PagedResult<StockPriceDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    public async Task<StockPriceDto?> GetLatestAppleStockDataAsync(CancellationToken cancellationToken = default)
    {
        var latest = await _dbContext.StockPrices
            .AsNoTracking()
            .Where(s => s.Symbol == Symbol)
            .OrderByDescending(s => s.PriceDate)
            .ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return latest is null ? null : ToDto(latest);
    }

    public async Task<StockPriceDto?> GetStockByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var target = date.ToDateTime(TimeOnly.MinValue);

        var match = await _dbContext.StockPrices
            .AsNoTracking()
            .Where(s => s.Symbol == Symbol && s.PriceDate == target)
            .FirstOrDefaultAsync(cancellationToken);

        return match is null ? null : ToDto(match);
    }

    // ----- helpers -------------------------------------------------------

    private string BuildRequestUri(bool useFullHistory)
    {
        // Built programmatically; the key is supplied via configuration, never hard-coded.
        var outputSize = useFullHistory ? "full" : "compact";
        var baseUrl = _alphaVantage.BaseUrl.TrimEnd('?');
        var queryString =
            $"function=TIME_SERIES_DAILY&symbol={Uri.EscapeDataString(Symbol)}" +
            $"&outputsize={outputSize}&apikey={Uri.EscapeDataString(_alphaVantage.ApiKey)}";
        return $"{baseUrl}?{queryString}";
    }

    private AlphaVantageResponse DeserializeAndValidate(string payload)
    {
        AlphaVantageResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<AlphaVantageResponse>(payload, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Alpha Vantage response for {Symbol}", Symbol);
            throw new InvalidOperationException("Alpha Vantage returned a response that could not be parsed.", ex);
        }

        if (parsed is null)
        {
            throw new InvalidOperationException("Alpha Vantage returned an empty response.");
        }

        // Alpha Vantage reports problems through these fields with HTTP 200.
        if (!string.IsNullOrWhiteSpace(parsed.ErrorMessage))
        {
            throw new InvalidOperationException($"Alpha Vantage error: {parsed.ErrorMessage}");
        }

        if (!string.IsNullOrWhiteSpace(parsed.Information))
        {
            // Typically an invalid API key or a throttling notice.
            throw new InvalidOperationException($"Alpha Vantage: {parsed.Information}");
        }

        if (!string.IsNullOrWhiteSpace(parsed.Note))
        {
            // Typically the free-tier rate limit.
            throw new InvalidOperationException($"Alpha Vantage rate limit reached: {parsed.Note}");
        }

        if (parsed.TimeSeries is null || parsed.TimeSeries.Count == 0)
        {
            throw new InvalidOperationException("Alpha Vantage returned no daily time-series data.");
        }

        return parsed;
    }

    private bool TryMap(string dateText, AlphaVantageDailyPrice daily, DateTime ingestedAt, out StockPrice stockPrice)
    {
        stockPrice = null!;

        if (!DateTime.TryParseExact(dateText, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var priceDate))
        {
            return false;
        }

        if (!TryParseDecimal(daily.Open, out var open) ||
            !TryParseDecimal(daily.High, out var high) ||
            !TryParseDecimal(daily.Low, out var low) ||
            !TryParseDecimal(daily.Close, out var close) ||
            !long.TryParse(daily.Volume, NumberStyles.Any, CultureInfo.InvariantCulture, out var volume))
        {
            return false;
        }

        stockPrice = new StockPrice
        {
            Symbol = Symbol,
            PriceDate = priceDate,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume,
            Source = SourceName,
            IngestedAtUtc = ingestedAt
        };
        return true;
    }

    private static bool TryParseDecimal(string value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result);

    private static IQueryable<StockPrice> ApplySearch(IQueryable<StockPrice> q, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return q;
        }

        var term = search.Trim();

        // If the term looks like a (partial) date, filter by the corresponding day range;
        // otherwise match against the symbol. Both run in the database.
        if (TryGetDateRange(term, out var from, out var to))
        {
            return q.Where(s => s.PriceDate >= from && s.PriceDate <= to);
        }

        var upper = term.ToUpper();
        return q.Where(s => s.Symbol.ToUpper().Contains(upper));
    }

    private static IQueryable<StockPrice> ApplyDateRange(IQueryable<StockPrice> q, DateOnly? fromDate, DateOnly? toDate)
    {
        if (fromDate.HasValue)
        {
            var from = fromDate.Value.ToDateTime(TimeOnly.MinValue);
            q = q.Where(s => s.PriceDate >= from);
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value.ToDateTime(TimeOnly.MinValue);
            q = q.Where(s => s.PriceDate <= to);
        }

        return q;
    }

    private static IQueryable<StockPrice> ApplySort(IQueryable<StockPrice> q, string? sortBy, string? sortDirection)
    {
        var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        switch ((sortBy ?? string.Empty).ToLowerInvariant())
        {
            case "close":
                q = descending ? q.OrderByDescending(s => s.Close) : q.OrderBy(s => s.Close);
                break;
            case "volume":
                q = descending ? q.OrderByDescending(s => s.Volume) : q.OrderBy(s => s.Volume);
                break;
            case "pricedate":
            default:
                q = descending ? q.OrderByDescending(s => s.PriceDate) : q.OrderBy(s => s.PriceDate);
                break;
        }

        // Deterministic tie-breaker so paging is stable.
        return ((IOrderedQueryable<StockPrice>)q).ThenByDescending(s => s.Id);
    }

    private static bool TryGetDateRange(string term, out DateTime from, out DateTime to)
    {
        from = default;
        to = default;

        if (YearOnly.IsMatch(term) &&
            int.TryParse(term, out var year) && year is >= 1 and <= 9999)
        {
            from = new DateTime(year, 1, 1);
            to = new DateTime(year, 12, 31);
            return true;
        }

        if (YearMonth.IsMatch(term) &&
            DateTime.TryParseExact(term + "-01", "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthStart))
        {
            from = monthStart;
            to = monthStart.AddMonths(1).AddDays(-1);
            return true;
        }

        if (YearMonthDay.IsMatch(term) &&
            DateTime.TryParseExact(term, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            from = day;
            to = day;
            return true;
        }

        return false;
    }

    private static StockPriceDto ToDto(StockPrice s) => new()
    {
        Symbol = s.Symbol,
        PriceDate = DateOnly.FromDateTime(s.PriceDate),
        Open = s.Open,
        High = s.High,
        Low = s.Low,
        Close = s.Close,
        Volume = s.Volume,
        Source = s.Source
    };
}
