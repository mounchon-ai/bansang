using Bansang.Application.Abstractions;
using Bansang.Domain.Catalog;
using Bansang.Domain.Inventory;
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

    public async Task LockSkusAsync(IEnumerable<Guid> skuIds, CancellationToken ct = default)
    {
        var ids = skuIds.Distinct().Order().ToArray();
        if (ids.Length == 0) return;
        await Database.ExecuteSqlAsync($"SELECT id FROM skus WHERE id = ANY({ids}) ORDER BY id FOR UPDATE", ct);
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
