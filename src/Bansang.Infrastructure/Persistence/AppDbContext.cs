using Bansang.Application.Abstractions;
using Bansang.Domain.Catalog;
using Bansang.Domain.Inventory;
using Bansang.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Bansang.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Sku> Skus => Set<Sku>();
    public DbSet<SkuUnit> SkuUnits => Set<SkuUnit>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<StockBucket> StockBuckets => Set<StockBucket>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockCount> StockCounts => Set<StockCount>();
    public DbSet<StockCountLine> StockCountLines => Set<StockCountLine>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();

    public async Task LockSkusAsync(IEnumerable<Guid> skuIds, CancellationToken ct = default)
    {
        var ids = skuIds.Distinct().Order().ToArray();
        if (ids.Length == 0) return;
        await Database.ExecuteSqlAsync($"SELECT id FROM skus WHERE id = ANY({ids}) ORDER BY id FOR UPDATE", ct);
    }

    public async Task LockSalesOrderAsync(Guid orderId, CancellationToken ct = default)
        => await Database.ExecuteSqlAsync($"SELECT id FROM sales_orders WHERE id = {orderId} FOR UPDATE", ct);

    public async Task<long> NextSequenceAsync(string prefix, string period, CancellationToken ct = default)
    {
        var values = await Database.SqlQuery<long>($"""
            INSERT INTO document_sequences (prefix, period, last_value) VALUES ({prefix}, {period}, 1)
            ON CONFLICT (prefix, period) DO UPDATE SET last_value = document_sequences.last_value + 1
            RETURNING last_value AS "Value"
            """).ToListAsync(ct);
        return values.Single();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
