using Bansang.Domain.Catalog;
using Bansang.Domain.Common;

namespace Bansang.Domain.Tests;

public class UnitConverterTests
{
    [Theory]
    [InlineData("ลัง", 9, 225, false)]
    [InlineData("ถุง", 3, 1.5, false)]
    [InlineData("kg", 2.345, 2.345, false)]
    [InlineData("ตัว", 50, 0.294, true)]     // 50 / 170 = 0.2941… → ปัด 3 ตำแหน่ง
    [InlineData("ตัว", 170, 1, true)]
    public void Converts_to_base_unit(string unit, decimal qty, decimal expectedBase, bool approx)
    {
        var sku = TestData.Nail();
        var c = sku.ToBase(unit, qty);
        Assert.Equal(expectedBase, c.QtyBase);
        Assert.Equal(approx, c.IsApproximate);
        Assert.Equal(qty, c.InputQty);
        Assert.Equal(sku.GetUnit(unit).FactorToBase, c.FactorSnapshot);
    }

    [Fact]
    public void Rounds_half_away_from_zero()
    {
        Assert.Equal(0.002m, Quantity.Round(0.0015m));
        Assert.Equal(-0.002m, Quantity.Round(-0.0015m));
    }

    [Fact]
    public void Quantity_must_be_positive()
    {
        var sku = TestData.Nail();
        Assert.Throws<DomainException>(() => sku.ToBase("kg", 0));
    }

    [Fact]
    public void Quantity_that_rounds_to_zero_is_rejected()
    {
        var sku = new Sku(Guid.NewGuid(), "S", "สกรูจิ๋ว", null, "kg", TrackingPattern.PackWeighApprox);
        sku.AddUnit("ตัว", 1m / 5000m, false, StockForm.Loose);
        var ex = Assert.Throws<DomainException>(() => sku.ToBase("ตัว", 1));
        Assert.Equal("qty_too_small", ex.Code);
    }

    [Fact]
    public void Unknown_unit_throws()
    {
        Assert.Throws<DomainException>(() => TestData.Nail().ToBase("เมตร", 1));
    }
}
