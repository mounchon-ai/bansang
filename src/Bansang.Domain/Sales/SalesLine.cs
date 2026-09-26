using Bansang.Domain.Catalog;
using Bansang.Domain.Common;

namespace Bansang.Domain.Sales;

/// <summary>
/// 1 บรรทัดในบิล — เก็บหน่วยที่ขาย + ยอดหน่วยฐาน ณ ตอนขาย (factor snapshot)
/// SkuId = null คือ "สินค้าเบ็ดเตล็ด" ใส่ราคาเอง ไม่ตัดสต็อก
/// </summary>
public class SalesLine
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public int LineNo { get; private set; }
    public Guid? SkuId { get; private set; }
    public string? SkuCode { get; private set; }
    public string Description { get; private set; } = null!;
    public string UnitName { get; private set; } = null!;
    public decimal Qty { get; private set; }
    public decimal UnitPrice { get; private set; }
    public PriceSource PriceSource { get; private set; }
    public decimal LineDiscount { get; private set; }
    /// <summary>ตัดสต็อกหรือไม่ (false = เบ็ดเตล็ด / สินค้าสั่งทำ)</summary>
    public bool TracksStock { get; private set; }
    public decimal QtyBase { get; private set; }
    public decimal? FactorSnapshot { get; private set; }
    public bool IsApproximate { get; private set; }
    public StockForm? Form { get; private set; }
    public decimal FulfilledQty { get; private set; }
    public decimal FulfilledBase { get; private set; }
    public decimal ReturnedQty { get; private set; }
    public decimal ReturnedBase { get; private set; }
    public decimal RefundedAmount { get; private set; }

    public decimal LineTotal => SalesMath.LineTotal(Qty, UnitPrice, LineDiscount);
    public decimal RemainingQty => Qty - FulfilledQty;
    public decimal RemainingBase => QtyBase - FulfilledBase;
    public decimal ReturnableQty => FulfilledQty - ReturnedQty;

    private SalesLine() { }

    internal SalesLine(Guid orderId, int lineNo) { Id = Guid.CreateVersion7(); OrderId = orderId; LineNo = lineNo; }

    internal void SetSku(Sku sku, string unitName, decimal qty, decimal unitPrice, PriceSource source, decimal discount)
    {
        if (!sku.IsActive) throw new DomainException("sku_inactive", $"{sku.Code} ปิดการขายอยู่");
        var unit = sku.GetUnit(unitName);
        if (!unit.CanSell) throw new DomainException("unit_not_sellable", $"หน่วย '{unit.UnitName}' ไม่ได้ตั้งให้ขาย");
        SalesMath.ValidateLine(qty, unitPrice, discount);
        SkuId = sku.Id;
        SkuCode = sku.Code;
        Description = sku.Name;
        UnitName = unit.UnitName;
        Qty = qty;
        UnitPrice = Quantity.RoundMoney(unitPrice);
        PriceSource = source;
        LineDiscount = Quantity.RoundMoney(discount);
        TracksStock = sku.IsStocked;
        if (TracksStock)
        {
            var conv = UnitConverter.ToBase(unit, qty);
            QtyBase = conv.QtyBase;
            FactorSnapshot = conv.FactorSnapshot;
            IsApproximate = conv.IsApproximate;
            Form = conv.Form;
        }
        else
        {
            QtyBase = 0;
            FactorSnapshot = null;
            IsApproximate = false;
            Form = null;
        }
    }

    internal void SetMisc(string description, string unitName, decimal qty, decimal unitPrice, decimal discount)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new DomainException("description_required", "ต้องระบุชื่อสินค้าเบ็ดเตล็ด");
        SalesMath.ValidateLine(qty, unitPrice, discount);
        SkuId = null;
        SkuCode = null;
        Description = description.Trim();
        UnitName = string.IsNullOrWhiteSpace(unitName) ? "ชิ้น" : unitName.Trim();
        Qty = qty;
        UnitPrice = Quantity.RoundMoney(unitPrice);
        PriceSource = PriceSource.Manual;
        LineDiscount = Quantity.RoundMoney(discount);
        TracksStock = false;
        QtyBase = 0;
    }

    /// <summary>ของที่ไม่ตัดสต็อก ถือว่าส่งมอบตอนยืนยันบิล</summary>
    internal void MarkDeliveredWithoutStock()
    {
        if (TracksStock) return;
        FulfilledQty = Qty;
    }

    /// <summary>รับของ qty หน่วย → คืนยอดหน่วยฐานที่ต้องตัด (รอบสุดท้ายใช้ยอดฐานที่เหลือทั้งหมด กันเศษ)</summary>
    internal decimal Fulfil(decimal qty)
    {
        Quantity.EnsurePositive(qty, "จำนวนที่รับ");
        if (!TracksStock) throw new DomainException("line_not_stocked", $"บรรทัด {LineNo} ไม่ใช่สินค้าที่ตัดสต็อก");
        if (qty > RemainingQty)
            throw new DomainException("fulfil_exceeds", $"บรรทัด {LineNo} รับได้อีกไม่เกิน {RemainingQty} {UnitName}");
        var qtyBase = qty == RemainingQty ? RemainingBase : Math.Min(Quantity.Round(qty * FactorSnapshot!.Value), RemainingBase);
        if (qtyBase <= 0) throw new DomainException("qty_too_small", "จำนวนที่รับเล็กเกินกว่าจะบันทึกเป็นหน่วยฐานได้");
        FulfilledQty += qty;
        FulfilledBase += qtyBase;
        return qtyBase;
    }

    /// <summary>คืนของ → (ยอดฐานที่คืนเข้าสต็อก, ยอดเงินคืน)</summary>
    internal (decimal QtyBase, decimal Refund) Return(decimal qty, decimal allocatedNet)
    {
        Quantity.EnsurePositive(qty, "จำนวนที่คืน");
        if (qty > ReturnableQty)
            throw new DomainException("return_exceeds", $"บรรทัด {LineNo} คืนได้ไม่เกิน {ReturnableQty} {UnitName} (เท่าที่รับไปแล้ว)");
        var all = qty == Qty - ReturnedQty;
        decimal qtyBase = 0;
        if (TracksStock)
        {
            var returnableBase = FulfilledBase - ReturnedBase;
            qtyBase = qty == ReturnableQty ? returnableBase : Math.Min(Quantity.Round(qty * FactorSnapshot!.Value), returnableBase);
        }
        var refund = all ? allocatedNet - RefundedAmount : Math.Min(Quantity.RoundMoney(allocatedNet / Qty * qty), allocatedNet - RefundedAmount);
        ReturnedQty += qty;
        ReturnedBase += qtyBase;
        RefundedAmount += refund;
        return (qtyBase, refund);
    }
}
