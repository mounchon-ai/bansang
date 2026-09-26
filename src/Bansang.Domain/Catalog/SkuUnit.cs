using Bansang.Domain.Common;

namespace Bansang.Domain.Catalog;

/// <summary>
/// หน่วยที่ซื้อ/ขาย SKU ได้ พร้อมสูตรแปลงเป็นหน่วยฐาน
/// IsExact = false หมายถึงสูตรประมาณ (เช่น 1 kg ≈ 170 ตัว) ต้องเคลียร์ส่วนต่างตอนนับ
/// Form = กองที่หน่วยนี้ดึงสต็อก (ลัง → Sealed, kg/ถุง/ตัว → Loose)
/// </summary>
public class SkuUnit
{
    public Guid Id { get; private set; }
    public Guid SkuId { get; private set; }
    public string UnitName { get; private set; } = null!;
    public decimal FactorToBase { get; private set; }
    public bool IsExact { get; private set; }
    public StockForm Form { get; private set; }
    public string? Barcode { get; private set; }
    public bool CanSell { get; private set; }
    public bool CanBuy { get; private set; }
    public bool IsBase { get; private set; }

    private SkuUnit() { }

    internal SkuUnit(Guid skuId, string unitName, decimal factorToBase, bool isExact, StockForm form,
        string? barcode, bool canSell, bool canBuy, bool isBase)
    {
        if (string.IsNullOrWhiteSpace(unitName)) throw new DomainException("unit_name_required", "ต้องระบุชื่อหน่วย");
        Id = Guid.CreateVersion7();
        SkuId = skuId;
        UnitName = unitName.Trim();
        IsBase = isBase;
        SetFactor(factorToBase, isExact);
        Form = form;
        Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
        CanSell = canSell;
        CanBuy = canBuy;
    }

    internal void SetFactor(decimal factorToBase, bool isExact)
    {
        if (factorToBase <= 0) throw new DomainException("factor_not_positive", "สูตรแปลงหน่วยต้องมากกว่า 0");
        if (IsBase && (factorToBase != 1m || !isExact))
            throw new DomainException("base_unit_factor", "หน่วยฐานต้องมี factor = 1 และเป็นสูตรแน่นอนเสมอ");
        FactorToBase = factorToBase;
        IsExact = isExact;
    }

    internal void Update(StockForm form, string? barcode, bool canSell, bool canBuy)
    {
        Form = form;
        Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
        CanSell = canSell;
        CanBuy = canBuy;
    }

    /// <summary>จำนวนหน่วยนี้ต่อ 1 หน่วยฐาน (เช่น ≈170 ตัว/kg) — ไว้แสดงผล</summary>
    public decimal UnitsPerBase => 1m / FactorToBase;
}
