using Bansang.Domain.Common;

namespace Bansang.Domain.Catalog;

/// <summary>ราคาต่อหน่วย ตั้งแยกต่อ (หน่วย × ระดับลูกค้า × จำนวนขั้นต่ำ) — ไม่คำนวณจากหน่วยอื่น</summary>
public class PriceRule
{
    public Guid Id { get; private set; }
    public Guid SkuId { get; private set; }
    public string UnitName { get; private set; } = null!;
    public CustomerTier Tier { get; private set; }
    public decimal MinQty { get; private set; }
    public decimal Price { get; private set; }

    private PriceRule() { }

    internal PriceRule(Guid skuId, string unitName, CustomerTier tier, decimal minQty, decimal price)
    {
        Id = Guid.CreateVersion7();
        SkuId = skuId;
        UnitName = unitName;
        Tier = tier;
        MinQty = minQty;
        SetPrice(price);
    }

    internal void SetPrice(decimal price)
    {
        if (price < 0) throw new DomainException("price_negative", "ราคาต้องไม่ติดลบ");
        Price = Quantity.RoundMoney(price);
    }
}
