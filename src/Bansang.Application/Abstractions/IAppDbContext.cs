using Bansang.Domain.Catalog;
using Bansang.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Bansang.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Product> Products { get; }
    DbSet<Sku> Skus { get; }
    DbSet<SkuUnit> SkuUnits { get; }
    DbSet<Location> Locations { get; }
    DbSet<StockBucket> StockBuckets { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<StockCount> StockCounts { get; }
    DbSet<StockCountLine> StockCountLines { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// ล็อกแถว SKU (SELECT … FOR UPDATE) เรียงตาม id — ทุกการเปลี่ยนสต็อกของ SKU เดียวกันจะเข้าคิวกัน
    /// กันสองเครื่อง POS ตัดกองเดียวกันพร้อมกัน ต้องเรียกภายใน transaction
    /// </summary>
    Task LockSkusAsync(IEnumerable<Guid> skuIds, CancellationToken ct = default);
}
