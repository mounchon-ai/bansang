using Bansang.Domain.Catalog;
using Bansang.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bansang.Infrastructure.Persistence;

internal static class Precision
{
    /// <summary>ปริมาณหน่วยฐาน: ทศนิยม 3 ตำแหน่ง</summary>
    public static PropertyBuilder<T> Qty<T>(this PropertyBuilder<T> p) => p.HasPrecision(18, 3);
    public static PropertyBuilder<T> Cost<T>(this PropertyBuilder<T> p) => p.HasPrecision(18, 4);
    public static PropertyBuilder<T> Money<T>(this PropertyBuilder<T> p) => p.HasPrecision(18, 2);
    /// <summary>สูตรแปลงหน่วย เช่น 1/170 ต้องเก็บละเอียดพอ</summary>
    public static PropertyBuilder<T> Factor<T>(this PropertyBuilder<T> p) => p.HasPrecision(28, 12);
}

internal sealed class ProductConfig : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Category).HasMaxLength(100);
        b.HasIndex(x => x.Name);
    }
}

internal sealed class SkuConfig : IEntityTypeConfiguration<Sku>
{
    public void Configure(EntityTypeBuilder<Sku> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Spec).HasMaxLength(200);
        b.Property(x => x.BaseUnit).HasMaxLength(30);
        b.Property(x => x.AvgCostPerBase).Cost();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.ProductId);
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Units).WithOne().HasForeignKey(u => u.SkuId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.PriceRules).WithOne().HasForeignKey(p => p.SkuId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Units).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.PriceRules).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SkuUnitConfig : IEntityTypeConfiguration<SkuUnit>
{
    public void Configure(EntityTypeBuilder<SkuUnit> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.UnitName).HasMaxLength(30);
        b.Property(x => x.FactorToBase).Factor();
        b.Property(x => x.Barcode).HasMaxLength(64);
        b.HasIndex(x => new { x.SkuId, x.UnitName }).IsUnique();
        b.HasIndex(x => x.Barcode).IsUnique().HasFilter("barcode IS NOT NULL");
    }
}

internal sealed class PriceRuleConfig : IEntityTypeConfiguration<PriceRule>
{
    public void Configure(EntityTypeBuilder<PriceRule> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.UnitName).HasMaxLength(30);
        b.Property(x => x.MinQty).Qty();
        b.Property(x => x.Price).Money();
        b.HasIndex(x => new { x.SkuId, x.UnitName, x.Tier, x.MinQty }).IsUnique();
    }
}

internal sealed class LocationConfig : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.Name).HasMaxLength(100);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class StockBucketConfig : IEntityTypeConfiguration<StockBucket>
{
    public void Configure(EntityTypeBuilder<StockBucket> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.QtyOnHandBase).Qty();
        b.Property(x => x.QtyReservedBase).Qty();
        b.HasIndex(x => new { x.SkuId, x.LocationId, x.Form }).IsUnique();
        b.HasIndex(x => new { x.LocationId, x.NeedsCount });
        b.HasOne<Sku>().WithMany().HasForeignKey(x => x.SkuId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockMovementConfig : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.QtyBase).Qty();
        b.Property(x => x.BalanceAfterBase).Qty();
        b.Property(x => x.InputQty).Qty();
        b.Property(x => x.FactorSnapshot).Factor();
        b.Property(x => x.CostPerBase).Cost();
        b.Property(x => x.InputUnit).HasMaxLength(30);
        b.Property(x => x.RefDocument).HasMaxLength(64);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.UserId).HasMaxLength(100);
        b.HasIndex(x => new { x.SkuId, x.At });
        b.HasIndex(x => x.BucketId);
        b.HasIndex(x => x.RefDocument);
        b.HasIndex(x => x.CorrelationId);
        // กลับรายการเดียวกันได้ครั้งเดียว — กันที่ระดับ DB ด้วย
        b.HasIndex(x => x.ReversalOfId).IsUnique().HasFilter("reversal_of_id IS NOT NULL");
        b.HasOne<StockBucket>().WithMany().HasForeignKey(x => x.BucketId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockCountConfig : IEntityTypeConfiguration<StockCount>
{
    public void Configure(EntityTypeBuilder<StockCount> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.ThresholdPercent).HasPrecision(5, 2);
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.HasIndex(x => new { x.LocationId, x.CreatedAt });
        b.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.CountId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class StockCountLineConfig : IEntityTypeConfiguration<StockCountLine>
{
    public void Configure(EntityTypeBuilder<StockCountLine> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.SystemQtyBase).Qty();
        b.Property(x => x.CountedQtyBase).Qty();
        b.Property(x => x.CountedInputQty).Qty();
        b.Property(x => x.VarianceBase).Qty();
        b.Property(x => x.VariancePercent).HasPrecision(9, 2);
        b.Property(x => x.CountedUnit).HasMaxLength(30);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.CountedBy).HasMaxLength(100);
        b.Property(x => x.ResolvedBy).HasMaxLength(100);
        b.HasIndex(x => new { x.SkuId, x.CountedAt });
        b.HasOne<Sku>().WithMany().HasForeignKey(x => x.SkuId).OnDelete(DeleteBehavior.Restrict);
    }
}
