using AppleStockAPI.Data;
using AppleStockAPI.Options;
using AppleStockAPI.Services;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ----- Configuration (strongly typed) --------------------------------------
builder.Services.Configure<AlphaVantageOptions>(
    builder.Configuration.GetSection(AlphaVantageOptions.SectionName));
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));

// ----- Database provider selection -----------------------------------------
// A single "Database:Provider" value chooses the EF Core provider. Nothing else in
// the application needs to change when switching between SQL Server and SQLite.
var databaseProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";

switch (databaseProvider.ToLowerInvariant())
{
    case "sqlserver":
        builder.Services.AddDbContext<StockDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));
        break;

    case "sqlite":
        builder.Services.AddDbContext<StockDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("Sqlite")));
        break;

    default:
        throw new InvalidOperationException(
            $"Unsupported database provider: '{databaseProvider}'. Use 'SqlServer' or 'Sqlite'.");
}

// ----- Application services -------------------------------------------------
// Typed HttpClient: the service never news-up HttpClient itself.
builder.Services.AddHttpClient<IStockDataService, StockDataService>();

builder.Services.AddControllers();

// OpenAPI document + Scalar interactive UI.
builder.Services.AddOpenApi();

var app = builder.Build();

// ----- Database initialisation ---------------------------------------------
// SQL Server uses EF Core migrations (InitialCreate). SQLite creates the schema
// directly, so it can run on any machine with no migration tooling.
await InitialiseDatabaseAsync(app, databaseProvider);

// ----- HTTP pipeline --------------------------------------------------------
// Serve the static frontend (wwwroot/index.html) at "/".
app.UseDefaultFiles();
app.UseStaticFiles();

// OpenAPI + Scalar, available in every environment so the API can be demoed live.
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "Apple Stock API";
});

app.MapControllers();

app.Run();

static async Task InitialiseDatabaseAsync(WebApplication app, string databaseProvider)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var dbContext = services.GetRequiredService<StockDbContext>();

        if (string.Equals(databaseProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            // Applies pending migrations, creating the database and StockPrices table if needed.
            await dbContext.Database.MigrateAsync();
        }
        else
        {
            // SQLite: build the schema from the model (no SQL Server-specific migrations).
            await dbContext.Database.EnsureCreatedAsync();
        }

        logger.LogInformation("Database initialised using the {Provider} provider.", databaseProvider);
    }
    catch (Exception ex)
    {
        // Fail loudly but clearly if the database cannot be reached at startup.
        logger.LogError(ex, "Database initialisation failed for provider {Provider}.", databaseProvider);
        throw;
    }
}
