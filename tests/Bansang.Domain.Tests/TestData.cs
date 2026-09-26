using Bansang.Domain.Catalog;
using Bansang.Domain.Inventory;

namespace Bansang.Domain.Tests;

internal static class TestData
{
    /// <summary>ตะปู 2 นิ้ว: หน่วยฐาน kg, ลัง = 25 kg, ถุง = 0.5 kg, 1 kg ≈ 170 ตัว</summary>
    public static Sku Nail()
    {
        var sku = new Sku(Guid.NewGuid(), "NAIL-2", "ตะปู 2 นิ้ว", "2 นิ้ว", "kg", TrackingPattern.PackWeighApprox);
        sku.AddUnit("ลัง", 25m, isExact: true, StockForm.Sealed);
        sku.AddUnit("ถุง", 0.5m, isExact: true, StockForm.Loose);
        sku.AddUnit("ตัว", 1m / 170m, isExact: false, StockForm.Loose);
        return sku;
    }

    public static (StockBucket Sealed, StockBucket Loose) Buckets(Sku sku)
    {
        var loc = Guid.NewGuid();
        return (new StockBucket(sku.Id, loc, StockForm.Sealed), new StockBucket(sku.Id, loc, StockForm.Loose));
    }

    public static MovementContext Ctx(string? reason = null) => new() { UserId = "tester", Reason = reason };
}
