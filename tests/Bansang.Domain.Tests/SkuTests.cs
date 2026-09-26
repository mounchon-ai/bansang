using Bansang.Domain.Catalog;
using Bansang.Domain.Common;

namespace Bansang.Domain.Tests;

public class SkuTests
{
    [Fact]
    public void New_sku_has_exactly_one_base_unit_with_factor_one()
    {
        var sku = TestData.Nail();
        var baseUnit = Assert.Single(sku.Units, u => u.IsBase);
        Assert.Equal("kg", baseUnit.UnitName);
        Assert.Equal(1m, baseUnit.FactorToBase);
        Assert.True(baseUnit.IsExact);
    }

    [Fact]
    public void Base_unit_factor_cannot_change()
    {
        var sku = TestData.Nail();
        var ex = Assert.Throws<DomainException>(() => sku.ChangeUnitFactor("kg", 2m, true));
        Assert.Equal("base_unit_factor", ex.Code);
    }

    [Fact]
    public void Duplicate_unit_name_is_rejected_case_insensitive()
    {
        var sku = new Sku(Guid.NewGuid(), "BOLT", "น็อต", null, "pcs", TrackingPattern.ExactPack);
        sku.AddUnit("Box", 100, true, StockForm.Sealed);
        Assert.Throws<DomainException>(() => sku.AddUnit("box", 50, true, StockForm.Sealed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Factor_must_be_positive(decimal factor)
    {
        var sku = TestData.Nail();
        Assert.Throws<DomainException>(() => sku.AddUnit("กระสอบ", factor, true, StockForm.Sealed));
    }

    [Fact]
    public void Cannot_remove_base_unit()
    {
        var sku = TestData.Nail();
        Assert.Throws<DomainException>(() => sku.RemoveUnit("kg"));
    }

    [Fact]
    public void Price_is_set_per_unit_and_picks_highest_min_qty_tier()
    {
        var sku = TestData.Nail();
        sku.SetPrice("kg", CustomerTier.Retail, 0, 60);
        sku.SetPrice("kg", CustomerTier.Retail, 10, 55);
        sku.SetPrice("ลัง", CustomerTier.Retail, 0, 1200); // ยกลังถูกกว่า 25 × 60 — ตั้งแยก ไม่คำนวณจาก kg
        sku.SetPrice("kg", CustomerTier.Technician, 0, 52);

        Assert.Equal(60, sku.ResolvePrice("kg", CustomerTier.Retail, 3)!.Price);
        Assert.Equal(55, sku.ResolvePrice("kg", CustomerTier.Retail, 12)!.Price);
        Assert.Equal(52, sku.ResolvePrice("kg", CustomerTier.Technician, 12)!.Price);
        Assert.Equal(1200, sku.ResolvePrice("ลัง", CustomerTier.Contractor, 1)!.Price); // ไม่มีราคาผู้รับเหมา → ใช้ราคาปลีก
        Assert.Null(sku.ResolvePrice("ถุง", CustomerTier.Retail, 1));
    }

    [Fact]
    public void Setting_same_price_key_updates_existing_rule()
    {
        var sku = TestData.Nail();
        sku.SetPrice("kg", CustomerTier.Retail, 0, 60);
        sku.SetPrice("kg", CustomerTier.Retail, 0, 65);
        Assert.Single(sku.PriceRules);
        Assert.Equal(65, sku.PriceRules[0].Price);
    }

    [Fact]
    public void Receipt_cost_is_weighted_average()
    {
        var sku = TestData.Nail();
        sku.ApplyReceiptCost(0, 225, 36);         // 9 ลัง × 900 = 225 kg @36
        Assert.Equal(36m, sku.AvgCostPerBase);
        sku.ApplyReceiptCost(225, 25, 40);        // +1 ลัง @1000 → (225×36 + 25×40) / 250
        Assert.Equal(36.4m, sku.AvgCostPerBase);
    }

    [Fact]
    public void Smallest_sealed_unit_is_used_for_break_bulk()
    {
        var sku = TestData.Nail();
        sku.AddUnit("พาเลท", 1000, true, StockForm.Sealed);
        Assert.Equal("ลัง", sku.SmallestSealedUnit!.UnitName);
    }
}
