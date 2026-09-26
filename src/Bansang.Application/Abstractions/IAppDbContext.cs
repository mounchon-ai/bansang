using Bansang.Domain.Catalog;
using Bansang.Domain.Inventory;
using Bansang.Domain.Sales;
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
    DbSet<Customer> Customers { get; }
    DbSet<SalesOrder> SalesOrders { get; }
    DbSet<Quotation> Quotations { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// ล็อกแถว SKU (SELECT … FOR UPDATE) เรียงตาม id — ทุกการเปลี่ยนสต็อกของ SKU เดียวกันจะเข้าคิวกัน
    /// กันสองเครื่อง POS ตัดกองเดียวกันพร้อมกัน ต้องเรียกภายใน transaction
    /// </summary>
    Task LockSkusAsync(IEnumerable<Guid> skuIds, CancellationToken ct = default);

    /// <summary>ล็อกแถวบิลขาย กันยืนยัน/รับของ/ยกเลิกบิลเดียวกันพร้อมกัน ต้องเรียกภายใน transaction</summary>
    Task LockSalesOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>เลขลำดับถัดไปของ (prefix, งวด) — อยู่ใน transaction เดียวกับเอกสาร rollback แล้วเลขไม่ข้าม</summary>
    Task<long> NextSequenceAsync(string prefix, string period, CancellationToken ct = default);
}
