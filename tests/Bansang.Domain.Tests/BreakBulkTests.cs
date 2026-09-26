using Bansang.Domain.Catalog;
using Bansang.Domain.Common;
using Bansang.Domain.Inventory;

namespace Bansang.Domain.Tests;

public class BreakBulkTests
{
    [Fact]
    public void Break_bulk_is_balanced_and_keeps_cost()
    {
        var sku = TestData.Nail();
        sku.ApplyReceiptCost(0, 100, 36);
        var (sealedB, loose) = TestData.Buckets(sku);
        sealedB.Post(MovementType.Receive, 100, TestData.Ctx()); // 4 ลัง

        var r = BreakBulk.Execute(sku, sku.GetUnit("ลัง"), 1, sealedB, loose, TestData.Ctx());

        Assert.Equal(-25, r.Out.QtyBase);
        Assert.Equal(25, r.In.QtyBase);
        Assert.Equal(0, r.Out.QtyBase + r.In.QtyBase);
        Assert.Equal(r.Out.CorrelationId, r.In.CorrelationId);
        Assert.NotNull(r.Out.CorrelationId);
        Assert.Equal(36, r.Out.CostPerBase);
        Assert.Equal(36, r.In.CostPerBase);
        Assert.Equal(75, sealedB.QtyOnHandBase);
        Assert.Equal(25, loose.QtyOnHandBase);
        Assert.Equal(36, sku.AvgCostPerBase);
    }

    [Fact]
    public void Cannot_break_more_boxes_than_available()
    {
        var sku = TestData.Nail();
        var (sealedB, loose) = TestData.Buckets(sku);
        sealedB.Post(MovementType.Receive, 25, TestData.Ctx());
        Assert.Throws<InsufficientStockException>(() =>
            BreakBulk.Execute(sku, sku.GetUnit("ลัง"), 2, sealedB, loose, TestData.Ctx()));
        Assert.Equal(25, sealedB.QtyOnHandBase);
        Assert.Equal(0, loose.QtyOnHandBase);
    }

    [Theory]
    [InlineData("ถุง")]  // ไม่ใช่หน่วยปิดผนึก
    [InlineData("kg")]
    public void Only_sealed_units_can_be_broken(string unit)
    {
        var sku = TestData.Nail();
        var (sealedB, loose) = TestData.Buckets(sku);
        sealedB.Post(MovementType.Receive, 100, TestData.Ctx());
        Assert.Throws<DomainException>(() => BreakBulk.Execute(sku, sku.GetUnit(unit), 1, sealedB, loose, TestData.Ctx()));
    }

    [Fact]
    public void Break_count_must_be_whole_number()
    {
        var sku = TestData.Nail();
        var (sealedB, loose) = TestData.Buckets(sku);
        sealedB.Post(MovementType.Receive, 100, TestData.Ctx());
        Assert.Throws<DomainException>(() => BreakBulk.Execute(sku, sku.GetUnit("ลัง"), 1.5m, sealedB, loose, TestData.Ctx()));
    }

    [Fact]
    public void Buckets_must_be_same_location()
    {
        var sku = TestData.Nail();
        var sealedB = new StockBucket(sku.Id, Guid.NewGuid(), StockForm.Sealed);
        var loose = new StockBucket(sku.Id, Guid.NewGuid(), StockForm.Loose);
        sealedB.Post(MovementType.Receive, 100, TestData.Ctx());
        Assert.Throws<DomainException>(() => BreakBulk.Execute(sku, sku.GetUnit("ลัง"), 1, sealedB, loose, TestData.Ctx()));
    }
}
