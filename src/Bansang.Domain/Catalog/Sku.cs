using Bansang.Domain.Common;

namespace Bansang.Domain.Catalog;

/// <summary>
/// SKU = สินค้าหนึ่งสเปก (เช่น ตะปู 2 นิ้ว) พร้อมหน่วยทั้งหมดและราคา
/// สต็อกเก็บเป็น "หน่วยฐาน" เสมอ — หน่วยที่นับหรือชั่งได้แม่นที่สุด
/// </summary>
public class Sku
{
    private readonly List<SkuUnit> _units = [];
    private readonly List<PriceRule> _priceRules = [];

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Spec { get; private set; }
    public string BaseUnit { get; private set; } = null!;
    public TrackingPattern TrackingPattern { get; private set; }
    public decimal AvgCostPerBase { get; private set; }
    public bool IsActive { get; private set; } = true;

    public IReadOnlyList<SkuUnit> Units => _units;
    public IReadOnlyList<PriceRule> PriceRules => _priceRules;

    /// <summary>สินค้าสั่งทำ (F) ไม่เก็บสต็อก</summary>
    public bool IsStocked => TrackingPattern != TrackingPattern.MadeToOrder;

    private Sku() { }

    public Sku(Guid productId, string code, string name, string? spec, string baseUnit,
        TrackingPattern pattern, StockForm baseForm = StockForm.Loose, string? baseBarcode = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("code_required", "ต้องระบุรหัส SKU");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("name_required", "ต้องระบุชื่อ SKU");
        if (string.IsNullOrWhiteSpace(baseUnit)) throw new DomainException("base_unit_required", "ต้องระบุหน่วยฐาน");

        Id = Guid.CreateVersion7();
        ProductId = productId;
        Code = code.Trim();
        Name = name.Trim();
        Spec = string.IsNullOrWhiteSpace(spec) ? null : spec.Trim();
        BaseUnit = baseUnit.Trim();
        TrackingPattern = pattern;
        _units.Add(new SkuUnit(Id, BaseUnit, 1m, true, baseForm, baseBarcode, canSell: true, canBuy: true, isBase: true));
    }

    public void UpdateInfo(string name, string? spec, TrackingPattern pattern, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("name_required", "ต้องระบุชื่อ SKU");
        Name = name.Trim();
        Spec = string.IsNullOrWhiteSpace(spec) ? null : spec.Trim();
        TrackingPattern = pattern;
        IsActive = isActive;
    }

    public SkuUnit BaseSkuUnit => _units.Single(u => u.IsBase);

    public SkuUnit AddUnit(string unitName, decimal factorToBase, bool isExact, StockForm form,
        string? barcode = null, bool canSell = true, bool canBuy = true)
    {
        if (FindUnit(unitName) is not null)
            throw new DomainException("unit_duplicate", $"SKU นี้มีหน่วย '{unitName}' อยู่แล้ว");
        var unit = new SkuUnit(Id, unitName, factorToBase, isExact, form, barcode, canSell, canBuy, isBase: false);
        _units.Add(unit);
        return unit;
    }

    public void UpdateUnit(string unitName, StockForm form, string? barcode, bool canSell, bool canBuy)
        => GetUnit(unitName).Update(form, barcode, canSell, canBuy);

    /// <summary>
    /// แก้สูตรแปลงหน่วย (เช่น 1 kg ≈ 170 → 165 ตัว) — ประวัติเดิมไม่เพี้ยน
    /// เพราะทุก StockMovement เก็บ factor ณ วันนั้นไว้แล้ว
    /// </summary>
    public void ChangeUnitFactor(string unitName, decimal factorToBase, bool isExact)
        => GetUnit(unitName).SetFactor(factorToBase, isExact);

    public void RemoveUnit(string unitName)
    {
        var unit = GetUnit(unitName);
        if (unit.IsBase) throw new DomainException("base_unit_remove", "ลบหน่วยฐานไม่ได้");
        _units.Remove(unit);
        _priceRules.RemoveAll(p => string.Equals(p.UnitName, unit.UnitName, StringComparison.OrdinalIgnoreCase));
    }

    public SkuUnit? FindUnit(string unitName)
        => _units.FirstOrDefault(u => string.Equals(u.UnitName, unitName.Trim(), StringComparison.OrdinalIgnoreCase));

    public SkuUnit GetUnit(string unitName)
        => FindUnit(unitName) ?? throw new DomainException("unit_not_found", $"SKU {Code} ไม่มีหน่วย '{unitName}'");

    /// <summary>หน่วยปิดผนึกที่เล็กที่สุด — ใช้ตอนระบบเสนอแกะลัง</summary>
    public SkuUnit? SmallestSealedUnit
        => _units.Where(u => u.Form == StockForm.Sealed && !u.IsBase && u.IsExact)
                 .OrderBy(u => u.FactorToBase)
                 .FirstOrDefault();

    public ConvertedQty ToBase(string unitName, decimal qty) => UnitConverter.ToBase(GetUnit(unitName), qty);

    // ---------- ราคา ----------

    public PriceRule SetPrice(string unitName, CustomerTier tier, decimal minQty, decimal price)
    {
        var unit = GetUnit(unitName);
        if (minQty < 0) throw new DomainException("min_qty_negative", "จำนวนขั้นต่ำต้องไม่ติดลบ");
        var existing = _priceRules.FirstOrDefault(p =>
            p.UnitName == unit.UnitName && p.Tier == tier && p.MinQty == minQty);
        if (existing is not null)
        {
            existing.SetPrice(price);
            return existing;
        }
        var rule = new PriceRule(Id, unit.UnitName, tier, minQty, price);
        _priceRules.Add(rule);
        return rule;
    }

    public void RemovePrice(string unitName, CustomerTier tier, decimal minQty)
        => _priceRules.RemoveAll(p =>
            string.Equals(p.UnitName, unitName, StringComparison.OrdinalIgnoreCase) && p.Tier == tier && p.MinQty == minQty);

    /// <summary>
    /// หาราคาต่อหน่วย: ใช้กฎของระดับลูกค้านั้นที่ MinQty สูงสุดที่ไม่เกินจำนวนซื้อ
    /// ถ้าระดับนั้นไม่มีกฎ ใช้ราคาปลีกแทน
    /// </summary>
    public PriceRule? ResolvePrice(string unitName, CustomerTier tier, decimal qty)
    {
        var unit = GetUnit(unitName);
        PriceRule? Pick(CustomerTier t) => _priceRules
            .Where(p => p.UnitName == unit.UnitName && p.Tier == t && p.MinQty <= qty)
            .OrderByDescending(p => p.MinQty)
            .FirstOrDefault();
        return Pick(tier) ?? (tier != CustomerTier.Retail ? Pick(CustomerTier.Retail) : null);
    }

    // ---------- ต้นทุน ----------

    /// <summary>ต้นทุนเฉลี่ยถ่วงน้ำหนัก (Moving Average) ตอนรับของเข้า</summary>
    public void ApplyReceiptCost(decimal onHandBeforeBase, decimal receivedBase, decimal costPerBase)
    {
        if (receivedBase <= 0) return;
        if (onHandBeforeBase <= 0 || AvgCostPerBase <= 0)
        {
            AvgCostPerBase = Quantity.RoundCost(costPerBase);
            return;
        }
        var total = onHandBeforeBase * AvgCostPerBase + receivedBase * costPerBase;
        AvgCostPerBase = Quantity.RoundCost(total / (onHandBeforeBase + receivedBase));
    }
}
