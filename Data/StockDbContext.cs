using AppleStockAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AppleStockAPI.Data;

/// <summary>
/// EF Core context for the stock data store. The same context is used whether the
/// configured provider is SQL Server or SQLite — only the provider registration in
/// Program.cs changes.
/// </summary>
public class StockDbContext : DbContext
{
    public StockDbContext(DbContextOptions<StockDbContext> options)
        : base(options)
    {
    }

    public DbSet<StockPrice> StockPrices => Set<StockPrice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StockPrice>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Symbol)
                .IsRequired()
                .HasMaxLength(16);

            entity.Property(x => x.Source)
                .IsRequired()
                .HasMaxLength(64);

            // decimal(18,4) is plenty for share prices and keeps SQL Server from
            // defaulting to (18,2) and silently truncating.
            entity.Property(x => x.Open).HasColumnType("decimal(18,4)");
            entity.Property(x => x.High).HasColumnType("decimal(18,4)");
            entity.Property(x => x.Low).HasColumnType("decimal(18,4)");
            entity.Property(x => x.Close).HasColumnType("decimal(18,4)");

            // A trading day for a given symbol must be unique. This makes repeated
            // ingestion idempotent and lets the database enforce de-duplication.
            entity.HasIndex(x => new { x.Symbol, x.PriceDate })
                .IsUnique();
        });
    }
}
