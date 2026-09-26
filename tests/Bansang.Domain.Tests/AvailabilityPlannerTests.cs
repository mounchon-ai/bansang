using Bansang.Domain.Catalog;
using Bansang.Domain.Inventory;

namespace Bansang.Domain.Tests;

public class AvailabilityPlannerTests
{
    private static readonly Sku Nail = TestData.Nail();

    [Fact]
    public void Doc_example_loose_1_2kg_sealed_4_boxes_needs_one_box()
    {
        // Sequence ในเอกสาร: ขอ 3 kg, Loose 1.2 kg, Sealed 4 ลัง → ขาด 1.8 kg แนะนำแกะ 1 ลัง
        var plan = AvailabilityPlanner.Plan(Nail, Nail.ToBase("kg", 3), 1.2m, 100, allowNegative: false);
        Assert.Equal(AvailabilityStatus.NeedsBreakBulk, plan.Status);
        Assert.Equal(1.8m, plan.ShortageBase);
        Assert.Equal("ลัง", plan.BreakBulkUnit);
        Assert.Equal(1, plan.BoxesToOpen);
    }

    [Fact]
    public void Sufficient_when_loose_is_enough()
    {
        var plan = AvailabilityPlanner.Plan(Nail, Nail.ToBase("ถุง", 2), 5, 0, false);
        Assert.Equal(AvailabilityStatus.Sufficient, plan.Status);
        Assert.Equal(0, plan.ShortageBase);
    }

    [Fact]
    public void Opens_multiple_boxes_when_needed()
    {
        var plan = AvailabilityPlanner.Plan(Nail, Nail.ToBase("kg", 60), 0, 100, false);
        Assert.Equal(AvailabilityStatus.NeedsBreakBulk, plan.Status);
        Assert.Equal(3, plan.BoxesToOpen);
    }

    [Theory]
    [InlineData(false, AvailabilityStatus.OutOfStock)]
    [InlineData(true, AvailabilityStatus.AllowedNegative)]
    public void No_boxes_depends_on_negative_policy(bool allowNegative, AvailabilityStatus expected)
    {
        var plan = AvailabilityPlanner.Plan(Nail, Nail.ToBase("kg", 3), 1, 0, allowNegative);
        Assert.Equal(expected, plan.Status);
        Assert.Equal(0, plan.BoxesToOpen);
    }

    [Fact]
    public void Selling_whole_boxes_without_enough_sealed_is_out_of_stock_even_if_negative_allowed()
    {
        var plan = AvailabilityPlanner.Plan(Nail, Nail.ToBase("ลัง", 2), 25, 0, allowNegative: true);
        Assert.Equal(AvailabilityStatus.OutOfStock, plan.Status);
        Assert.Equal(StockForm.Sealed, plan.Form);
    }

    [Fact]
    public void Approximate_request_is_flagged()
    {
        var plan = AvailabilityPlanner.Plan(Nail, Nail.ToBase("ตัว", 50), 1, 0, false);
        Assert.True(plan.IsApproximate);
        Assert.Equal(0.294m, plan.RequiredBase);
    }
}
