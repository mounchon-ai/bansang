using Bansang.Domain.Common;

namespace Bansang.Domain.Catalog;

/// <summary>ผลการแปลงหน่วย พร้อม factor ที่ใช้ (เก็บลง movement) และธงว่าเป็นค่าประมาณ</summary>
public readonly record struct ConvertedQty(decimal QtyBase, string InputUnit, decimal InputQty, decimal FactorSnapshot, bool IsApproximate, StockForm Form);

public static class UnitConverter
{
    public static ConvertedQty ToBase(SkuUnit unit, decimal qty)
    {
        Quantity.EnsurePositive(qty);
        var qtyBase = Quantity.Round(qty * unit.FactorToBase);
        if (qtyBase <= 0)
            throw new DomainException("qty_too_small", $"{qty} {unit.UnitName} เล็กเกินกว่าจะบันทึกเป็นหน่วยฐานได้ (ทศนิยม 3 ตำแหน่ง)");
        return new ConvertedQty(qtyBase, unit.UnitName, qty, unit.FactorToBase, !unit.IsExact, unit.Form);
    }

    public static decimal FromBase(SkuUnit unit, decimal qtyBase)
        => Quantity.Round(qtyBase / unit.FactorToBase);
}
