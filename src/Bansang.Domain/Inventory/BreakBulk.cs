using Bansang.Domain.Catalog;
using Bansang.Domain.Common;

namespace Bansang.Domain.Inventory;

public sealed record BreakBulkResult(StockMovement Out, StockMovement In, decimal QtyBase);

/// <summary>
/// แกะลัง = "แตกแบงก์": Sealed ลด / Loose เพิ่ม เท่ากันในหน่วยฐาน ต้นทุนต่อหน่วยฐานไม่เปลี่ยน
/// </summary>
public static class BreakBulk
{
    public static BreakBulkResult Execute(Sku sku, SkuUnit sealedUnit, decimal count,
        StockBucket sealedBucket, StockBucket looseBucket, MovementContext ctx)
    {
        if (sealedUnit.SkuId != sku.Id) throw new DomainException("unit_mismatch", "หน่วยไม่ใช่ของ SKU นี้");
        if (sealedUnit.Form != StockForm.Sealed)
            throw new DomainException("break_bulk_unit", $"หน่วย '{sealedUnit.UnitName}' ไม่ใช่หน่วยปิดผนึก แกะไม่ได้");
        if (!sealedUnit.IsExact)
            throw new DomainException("break_bulk_unit", "แกะลังได้เฉพาะหน่วยที่สูตรแปลงแน่นอน");
        if (sealedBucket.Form != StockForm.Sealed || looseBucket.Form != StockForm.Loose
            || sealedBucket.SkuId != sku.Id || looseBucket.SkuId != sku.Id
            || sealedBucket.LocationId != looseBucket.LocationId)
            throw new DomainException("bucket_mismatch", "กองต้นทาง/ปลายทางของการแกะลังไม่ถูกต้อง");
        if (count != decimal.Truncate(count) || count <= 0)
            throw new DomainException("break_bulk_count", "จำนวนที่แกะต้องเป็นจำนวนเต็มบวก");

        var converted = UnitConverter.ToBase(sealedUnit, count);
        var context = ctx with
        {
            InputUnit = converted.InputUnit,
            InputQty = converted.InputQty,
            FactorSnapshot = converted.FactorSnapshot,
            IsApproximate = false,
            CostPerBase = sku.AvgCostPerBase,
            CorrelationId = ctx.CorrelationId ?? Guid.CreateVersion7(),
        };

        // แกะลังไม่อนุญาตให้ Sealed ติดลบ — ต้องมีลังจริง
        var @out = sealedBucket.Post(MovementType.BreakBulkOut, -converted.QtyBase, context);
        var @in = looseBucket.Post(MovementType.BreakBulkIn, converted.QtyBase, context);
        return new BreakBulkResult(@out, @in, converted.QtyBase);
    }
}
