using Bansang.Domain.Catalog;
using Bansang.Domain.Common;
using Bansang.Domain.Inventory;

namespace Bansang.Domain.Tests;

public class StockCountTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Variance_within_threshold_is_auto_adjusted_and_above_needs_approval()
    {
        var count = new StockCount(Guid.NewGuid(), 3, "u", Now);
        var small = count.RecordLine(Guid.NewGuid(), StockForm.Loose, 20, 19.5m, "kg", 19.5m, "u", Now);  // −2.5%
        var big = count.RecordLine(Guid.NewGuid(), StockForm.Loose, 20, 18m, "kg", 18m, "u", Now);        // −10%
        var same = count.RecordLine(Guid.NewGuid(), StockForm.Sealed, 100, 100, "ลัง", 4, "u", Now);

        var auto = count.Submit(Now);

        Assert.Equal(-2.5m, small.VariancePercent);
        Assert.Equal(CountLineStatus.AutoAdjusted, small.Status);
        Assert.Equal(CountLineStatus.PendingApproval, big.Status);
        Assert.Equal(CountLineStatus.Matched, same.Status);
        Assert.Equal([small], auto);
        Assert.Equal(StockCountStatus.Submitted, count.Status);
    }

    [Fact]
    public void Zero_system_qty_with_counted_stock_exceeds_threshold()
    {
        var count = new StockCount(Guid.NewGuid(), 3, "u", Now);
        var line = count.RecordLine(Guid.NewGuid(), StockForm.Loose, 0, 1, null, null, "u", Now);
        count.Submit(Now);
        Assert.Equal(100, line.VariancePercent);
        Assert.Equal(CountLineStatus.PendingApproval, line.Status);
    }

    [Fact]
    public void Recount_replaces_line_and_submitted_count_is_locked()
    {
        var count = new StockCount(Guid.NewGuid(), 3, "u", Now);
        var sku = Guid.NewGuid();
        count.RecordLine(sku, StockForm.Loose, 10, 8, null, null, "u", Now);
        count.RecordLine(sku, StockForm.Loose, 10, 9.9m, null, null, "u", Now);
        Assert.Single(count.Lines);
        count.Submit(Now);
        Assert.Throws<DomainException>(() => count.RecordLine(sku, StockForm.Loose, 10, 9, null, null, "u", Now));
    }

    [Fact]
    public void Approval_requires_reason_and_pending_state()
    {
        var count = new StockCount(Guid.NewGuid(), 3, "u", Now);
        var line = count.RecordLine(Guid.NewGuid(), StockForm.Loose, 20, 10, null, null, "u", Now);
        Assert.Throws<DomainException>(() => line.Approve("x", Guid.NewGuid(), "owner", Now)); // ยังไม่ submit
        count.Submit(Now);
        Assert.Throws<DomainException>(() => line.Approve(" ", Guid.NewGuid(), "owner", Now));
        line.Approve("ของหาย", Guid.NewGuid(), "owner", Now);
        Assert.Equal(CountLineStatus.Approved, line.Status);
        Assert.Throws<DomainException>(() => line.Reject("x", "owner", Now));
    }

    [Fact]
    public void Empty_count_cannot_be_submitted()
    {
        Assert.Throws<DomainException>(() => new StockCount(Guid.NewGuid(), 3, "u", Now).Submit(Now));
    }
}
