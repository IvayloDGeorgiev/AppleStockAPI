using AppleStockAPI.DTOs;
using AppleStockAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppleStockAPI.Controllers;

/// <summary>
/// Read/ingest endpoints for Apple stock data. Retrieval endpoints read only from the
/// database; only the ingest endpoint talks to Alpha Vantage.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StocksController : ControllerBase
{
    private readonly IStockDataService _stockDataService;
    private readonly ILogger<StocksController> _logger;

    public StocksController(IStockDataService stockDataService, ILogger<StocksController> logger)
    {
        _stockDataService = stockDataService;
        _logger = logger;
    }

    /// <summary>
    /// Fetch the latest AAPL daily data from Alpha Vantage and store any new days.
    /// Running it again is safe: existing days are skipped.
    /// </summary>
    [HttpPost("ingest")]
    [ProducesResponseType(typeof(IngestionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Ingest(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _stockDataService.IngestAppleStockDataAsync(cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            // Configuration / upstream-API problems: surface a clean message, not a stack trace.
            _logger.LogWarning(ex, "Ingestion could not be completed");
            return Problem(
                title: "Ingestion failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    /// <summary>
    /// Return stored records with database-side search, date filtering, sorting and paging.
    /// Never calls Alpha Vantage.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<StockPriceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<StockPriceDto>>> Get(
        [FromQuery] StockQueryParameters query,
        CancellationToken cancellationToken)
    {
        var result = await _stockDataService.GetStocksAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Return the newest stored AAPL record, or 404 if the store is empty.</summary>
    [HttpGet("latest")]
    [ProducesResponseType(typeof(StockPriceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockPriceDto>> GetLatest(CancellationToken cancellationToken)
    {
        var latest = await _stockDataService.GetLatestAppleStockDataAsync(cancellationToken);
        if (latest is null)
        {
            return Problem(
                title: "No data",
                detail: "No stock records have been ingested yet. Run POST /api/stocks/ingest first.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Ok(latest);
    }

    /// <summary>Optional: return the AAPL record for a specific trading day (yyyy-MM-dd).</summary>
    [HttpGet("{date}")]
    [ProducesResponseType(typeof(StockPriceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockPriceDto>> GetByDate(string date, CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParse(date, out var parsedDate))
        {
            return Problem(
                title: "Invalid date",
                detail: $"'{date}' is not a valid date. Use the yyyy-MM-dd format.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var match = await _stockDataService.GetStockByDateAsync(parsedDate, cancellationToken);
        if (match is null)
        {
            return Problem(
                title: "Not found",
                detail: $"No stored record for {parsedDate:yyyy-MM-dd}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Ok(match);
    }
}
