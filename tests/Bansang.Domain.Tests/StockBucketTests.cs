using Bansang.Domain.Common;
using Bansang.Domain.Inventory;

namespace Bansang.Domain.Tests;

public class StockBucketTests
{
    [Fact]
    public void Balance_equals_sum_of_ledger()
    {
        var (_, loose) = TestData.Buckets(TestData.Nail());
        var ledger = new List<StockMovement>
        {
            loose.Post(MovementType.Receive, 10, TestData.Ctx()),
            loose.Post(MovementType.Issue, -3.25m, TestData.Ctx()),
            loose.Post(MovementType.Adjust, -0.1m, TestData.Ctx()),
            loose.Post(MovementType.Return, 1, TestData.Ctx()),
        };
        Assert.Equal(ledger.Sum(m => m.QtyBase), loose.QtyOnHandBase);
        Assert.Equal(7.65m, loose.QtyOnHandBase);
        Assert.Equal(7.65m, ledger[^1].BalanceAfterBase);
    }

    [Fact]
    public void Cannot_issue_more_than_available_when_negative_not_allowed()
    {
        var (_, loose) = TestData.Buckets(TestData.Nail());
        loose.Post(MovementType.Receive, 1.2m, TestData.Ctx());
        var ex = Assert.Throws<InsufficientStockException>(() => loose.Post(MovementType.Issue, -3, TestData.Ctx()));
        Assert.Equal(1.8m, ex.ShortageBase);
        Assert.Equal(1.2m, loose.QtyOnHandBase);
    }

    [Fact]
    public void Negative_sale_is_allowed_and_flags_for_count()
    {
        var (_, loose) = TestData.Buckets(TestData.Nail());
        loose.Post(MovementType.Issue, -2, TestData.Ctx(), allowNegative: true);
        Assert.Equal(-2, loose.QtyOnHandBase);
        Assert.True(loose.NeedsCount);
    }

    [Fact]
    public void Reserved_stock_cannot_be_sold_to_someone_else()
    {
        var (_, loose) = TestData.Buckets(TestData.Nail());
        loose.Post(MovementType.Receive, 10, TestData.Ctx());
        loose.Reserve(8);
        Assert.Equal(2, loose.QtyAvailableBase);
        Assert.Throws<InsufficientStockException>(() => loose.Post(MovementType.Issue, -3, TestData.Ctx()));
        Assert.Throws<InsufficientStockException>(() => loose.Reserve(3));
    }

    [Fact]
    public void Issue_from_reservation_reduces_both_on_hand_and_reserved()
    {
        var (_, loose) = TestData.Buckets(TestData.Nail());
        loose.Post(MovementType.Receive, 10, TestData.Ctx());
        loose.Reserve(8);
        loose.Post(MovementType.Issue, -5, TestData.Ctx(), consumeReserved: true); // ทยอยรับรอบแรก
        Assert.Equal(5, loose.QtyOnHandBase);
        Assert.Equal(3, loose.QtyReservedBase);
        Assert.Equal(2, loose.QtyAvailableBase);
        Assert.Throws<DomainException>(() => loose.Post(MovementType.Issue, -4, TestData.Ctx(), consumeReserved: true));
    }

    [Fact]
    public void Release_cannot_exceed_reserved()
    {
        var (_, loose) = TestData.Buckets(TestData.Nail());
        loose.Post(MovementType.Receive, 10, TestData.Ctx());
        loose.Reserve(2);
        Assert.Throws<DomainException>(() => loose.Release(3));
        loose.Release(2);
        Assert.Equal(0, loose.QtyReservedBase);
    }

    [Fact]
    public void Movement_keeps_factor_snapshot()
    {
        var sku = TestData.Nail();
        var (_, loose) = TestData.Buckets(sku);
        loose.Post(MovementType.Receive, 10, TestData.Ctx());
        var c = sku.ToBase("ตัว", 50);
        var m = loose.Post(MovementType.Issue, -c.QtyBase,
            TestData.Ctx() with { InputUnit = c.InputUnit, InputQty = c.InputQty, FactorSnapshot = c.FactorSnapshot, IsApproximate = true });

        sku.ChangeUnitFactor("ตัว", 1m / 165m, false); // แก้สูตรทีหลัง
        Assert.Equal(1m / 170m, m.FactorSnapshot);      // ประวัติไม่เพี้ยน
        Assert.True(m.IsApproximate);
    }
}
