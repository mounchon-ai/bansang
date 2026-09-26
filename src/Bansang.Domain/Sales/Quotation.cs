using Bansang.Domain.Catalog;
using Bansang.Domain.Common;

namespace Bansang.Domain.Sales;

/// <summary>ใบเสนอราคา — ล็อกราคาไว้จนถึง ValidUntil แล้วแปลงเป็นบิลขายได้</summary>
public class Quotation
{
    private readonly List<QuotationLine> _lines = [];

    public Guid Id { get; private set; }
    public string Number { get; private set; } = null!;
    public Guid? CustomerId { get; private set; }
    public CustomerTier Tier { get; private set; }
    public DateOnly ValidUntil { get; private set; }
    public QuotationStatus Status { get; private set; }
    public decimal BillDiscount { get; private set; }
    public decimal VatRatePercent { get; private set; }
    public string? Note { get; private set; }
    public Guid? SalesOrderId { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<QuotationLine> Lines => _lines;

    public decimal Subtotal => _lines.Sum(l => l.LineTotal);
    public decimal Total => Subtotal - BillDiscount;
    public decimal VatAmount => SalesMath.VatIncluded(Total, VatRatePercent);

    private Quotation() { }

    public Quotation(string number, Guid? customerId, CustomerTier tier, DateOnly validUntil, decimal billDiscount,
        decimal vatRatePercent, string userId, DateTimeOffset at, string? note = null)
    {
        if (billDiscount < 0) throw new DomainException("discount_negative", "ส่วนลดต้องไม่ติดลบ");
        Id = Guid.CreateVersion7();
        Number = number;
        CustomerId = customerId;
        Tier = tier;
        ValidUntil = validUntil;
        BillDiscount = Quantity.RoundMoney(billDiscount);
        VatRatePercent = vatRatePercent;
        Status = QuotationStatus.Open;
        CreatedBy = userId;
        CreatedAt = at;
        Note = note;
    }

    /// <summary>เพิ่มรายการ — ไม่ระบุราคาใช้ตารางราคาตามระดับลูกค้า</summary>
    public QuotationLine AddLine(Sku sku, string unit, decimal qty, decimal? price = null, decimal discount = 0)
    {
        EnsureOpen();
        var u = sku.GetUnit(unit);
        if (!u.CanSell) throw new DomainException("unit_not_sellable", $"หน่วย '{u.UnitName}' ไม่ได้ตั้งให้ขาย");
        var unitPrice = price ?? sku.ResolvePrice(unit, Tier, qty)?.Price
            ?? throw new DomainException("price_missing", $"{sku.Code} หน่วย '{unit}' ยังไม่ได้ตั้งราคา — กรอกราคาเอง");
        SalesMath.ValidateLine(qty, unitPrice, discount);
        var line = new QuotationLine(Id, _lines.Count + 1, sku.Id, sku.Code, sku.Name, u.UnitName, qty, unitPrice, discount);
        _lines.Add(line);
        return line;
    }

    public QuotationLine AddMiscLine(string description, string unit, decimal qty, decimal price, decimal discount = 0)
    {
        EnsureOpen();
        if (string.IsNullOrWhiteSpace(description)) throw new DomainException("description_required", "ต้องระบุชื่อสินค้า");
        SalesMath.ValidateLine(qty, price, discount);
        var line = new QuotationLine(Id, _lines.Count + 1, null, null, description.Trim(),
            string.IsNullOrWhiteSpace(unit) ? "ชิ้น" : unit.Trim(), qty, price, discount);
        _lines.Add(line);
        return line;
    }

    public bool IsExpired(DateOnly today) => today > ValidUntil;

    /// <summary>แปลงเป็นบิลขาย (Draft) โดยคงราคาที่เสนอไว้</summary>
    public SalesOrder ConvertToOrder(Guid locationId, FulfillmentMode mode, IReadOnlyDictionary<Guid, Sku> skus,
        DateOnly today, string userId, DateTimeOffset at)
    {
        EnsureOpen();
        if (IsExpired(today)) throw new DomainException("quotation_expired", $"ใบเสนอราคา {Number} หมดอายุแล้ว ({ValidUntil:yyyy-MM-dd})");
        if (_lines.Count == 0) throw new DomainException("quotation_empty", "ใบเสนอราคาไม่มีรายการ");

        var order = new SalesOrder(locationId, mode, CustomerId, Tier, VatRatePercent, userId, at, $"จากใบเสนอราคา {Number}", Id);
        foreach (var l in _lines.OrderBy(l => l.LineNo))
        {
            if (l.SkuId is { } skuId) order.AddLine(skus[skuId], l.UnitName, l.Qty, l.UnitPrice, l.LineDiscount, PriceSource.Quotation);
            else order.AddMiscLine(l.Description, l.UnitName, l.Qty, l.UnitPrice, l.LineDiscount);
        }
        order.Update(mode, BillDiscount, order.Note);
        Status = QuotationStatus.Converted;
        SalesOrderId = order.Id;
        return order;
    }

    public void Cancel()
    {
        EnsureOpen();
        Status = QuotationStatus.Cancelled;
    }

    private void EnsureOpen()
    {
        if (Status != QuotationStatus.Open) throw new DomainException("quotation_not_open", $"ใบเสนอราคาสถานะ {Status}");
    }
}

public class QuotationLine
{
    public Guid Id { get; private set; }
    public Guid QuotationId { get; private set; }
    public int LineNo { get; private set; }
    public Guid? SkuId { get; private set; }
    public string? SkuCode { get; private set; }
    public string Description { get; private set; } = null!;
    public string UnitName { get; private set; } = null!;
    public decimal Qty { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineDiscount { get; private set; }

    public decimal LineTotal => SalesMath.LineTotal(Qty, UnitPrice, LineDiscount);

    private QuotationLine() { }

    internal QuotationLine(Guid quotationId, int lineNo, Guid? skuId, string? skuCode, string description, string unitName,
        decimal qty, decimal unitPrice, decimal discount)
    {
        Id = Guid.CreateVersion7();
        QuotationId = quotationId;
        LineNo = lineNo;
        SkuId = skuId;
        SkuCode = skuCode;
        Description = description;
        UnitName = unitName;
        Qty = qty;
        UnitPrice = Quantity.RoundMoney(unitPrice);
        LineDiscount = Quantity.RoundMoney(discount);
    }
}
