using Bansang.Domain.Catalog;
using Bansang.Domain.Common;

namespace Bansang.Domain.Sales;

/// <summary>ยอดหน่วยฐานที่ต้องเคลื่อนสต็อกของ 1 บรรทัด (ให้ Application ไปจอง/ตัด/คืน)</summary>
public sealed record LineStockEffect(SalesLine Line, decimal Qty, decimal QtyBase);

public sealed record LineQty(Guid LineId, decimal Qty);

/// <summary>
/// บิลขาย — ขายหลายหน่วยในบิลเดียว, ราคาตามระดับลูกค้า
/// ยืนยันบิล = จองของ (Reserve), ของออกจากร้านจริง = ตัดสต็อก (Issue) ต่อรอบรับของ
/// </summary>
public class SalesOrder
{
    private readonly List<SalesLine> _lines = [];
    private readonly List<Payment> _payments = [];
    private readonly List<Fulfillment> _fulfillments = [];
    private readonly List<SalesReturn> _returns = [];

    public Guid Id { get; private set; }
    public string? Number { get; private set; }
    public Guid? CustomerId { get; private set; }
    public CustomerTier Tier { get; private set; }
    public Guid LocationId { get; private set; }
    public FulfillmentMode Mode { get; private set; }
    public SalesOrderStatus Status { get; private set; }
    public decimal BillDiscount { get; private set; }
    public decimal VatRatePercent { get; private set; }
    public decimal ChangeAmount { get; private set; }
    public string? Note { get; private set; }
    public Guid? QuotationId { get; private set; }
    public string? CancelReason { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public string? ConfirmedBy { get; private set; }

    public IReadOnlyList<SalesLine> Lines => _lines;
    public IReadOnlyList<Payment> Payments => _payments;
    public IReadOnlyList<Fulfillment> Fulfillments => _fulfillments;
    public IReadOnlyList<SalesReturn> Returns => _returns;

    public decimal Subtotal => _lines.Sum(l => l.LineTotal);
    public decimal Total => Subtotal - BillDiscount;
    public decimal VatAmount => SalesMath.VatIncluded(Total, VatRatePercent);
    public decimal TotalBeforeVat => Total - VatAmount;
    public decimal PaidAmount => _payments.Where(p => !p.IsRefund).Sum(p => p.Amount) - ChangeAmount;
    public decimal RefundedAmount => _payments.Where(p => p.IsRefund).Sum(p => p.Amount);

    private SalesOrder() { }

    public SalesOrder(Guid locationId, FulfillmentMode mode, Guid? customerId, CustomerTier tier, decimal vatRatePercent,
        string userId, DateTimeOffset at, string? note = null, Guid? quotationId = null)
    {
        if (vatRatePercent < 0) throw new DomainException("vat_negative", "อัตรา VAT ต้องไม่ติดลบ");
        Id = Guid.CreateVersion7();
        LocationId = locationId;
        Mode = mode;
        CustomerId = customerId;
        Tier = tier;
        VatRatePercent = vatRatePercent;
        Status = SalesOrderStatus.Draft;
        CreatedBy = userId;
        CreatedAt = at;
        Note = note;
        QuotationId = quotationId;
    }

    // ---------- Draft: แก้บรรทัด ----------

    /// <summary>เพิ่มสินค้า: ไม่ระบุราคา = ใช้ตารางราคาตามระดับลูกค้า; ระบุ = กรอกเอง</summary>
    public SalesLine AddLine(Sku sku, string unit, decimal qty, decimal? price = null, decimal discount = 0,
        PriceSource? source = null)
    {
        EnsureDraft();
        var line = new SalesLine(Id, NextLineNo());
        var (unitPrice, src) = Price(sku, unit, qty, price, source);
        line.SetSku(sku, unit, qty, unitPrice, src, discount);
        _lines.Add(line);
        return line;
    }

    /// <summary>สินค้าเบ็ดเตล็ด — ยังไม่ได้ลงระบบ ใส่ชื่อและราคาเอง ไม่ตัดสต็อก</summary>
    public SalesLine AddMiscLine(string description, string unit, decimal qty, decimal price, decimal discount = 0)
    {
        EnsureDraft();
        var line = new SalesLine(Id, NextLineNo());
        line.SetMisc(description, unit, qty, price, discount);
        _lines.Add(line);
        return line;
    }

    /// <summary>แก้บรรทัด (sku = null สำหรับสินค้าเบ็ดเตล็ด)</summary>
    public SalesLine UpdateLine(Guid lineId, Sku? sku, string unit, decimal qty, decimal? price, decimal discount)
    {
        EnsureDraft();
        var line = GetLine(lineId);
        if (line.SkuId is null)
        {
            if (price is null) throw new DomainException("price_required", "สินค้าเบ็ดเตล็ดต้องกรอกราคา");
            line.SetMisc(line.Description, unit, qty, price.Value, discount);
            return line;
        }
        if (sku is null || sku.Id != line.SkuId) throw new DomainException("sku_mismatch", "SKU ไม่ตรงกับบรรทัด");
        var (unitPrice, src) = Price(sku, unit, qty, price, null);
        line.SetSku(sku, unit, qty, unitPrice, src, discount);
        return line;
    }

    public void RemoveLine(Guid lineId)
    {
        EnsureDraft();
        _lines.Remove(GetLine(lineId));
    }

    /// <summary>เปลี่ยนลูกค้า → คิดราคาใหม่ทุกบรรทัดที่ใช้ตารางราคา</summary>
    public void ChangeCustomer(Guid? customerId, CustomerTier tier, IReadOnlyDictionary<Guid, Sku> skus)
    {
        EnsureDraft();
        CustomerId = customerId;
        Tier = tier;
        foreach (var line in _lines.Where(l => l.SkuId is not null && l.PriceSource == PriceSource.Rule))
        {
            var sku = skus[line.SkuId!.Value];
            var (price, _) = Price(sku, line.UnitName, line.Qty, null, null);
            line.SetSku(sku, line.UnitName, line.Qty, price, PriceSource.Rule, Math.Min(line.LineDiscount, Quantity.RoundMoney(line.Qty * price)));
        }
    }

    public void Update(FulfillmentMode mode, decimal billDiscount, string? note)
    {
        EnsureDraft();
        if (billDiscount < 0) throw new DomainException("discount_negative", "ส่วนลดต้องไม่ติดลบ");
        Mode = mode;
        BillDiscount = Quantity.RoundMoney(billDiscount);
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    // ---------- ยืนยัน + รับเงิน ----------

    /// <summary>
    /// ยืนยันบิล: ต้องจ่ายครบ (ยังไม่มีเครดิต) เงินทอนได้เฉพาะจากเงินสด
    /// คืนรายการที่ต้องจองสต็อก — Application ต้องจองให้ครบใน transaction เดียวกัน
    /// </summary>
    public IReadOnlyList<LineStockEffect> Confirm(string number, IReadOnlyList<PaymentInput> payments, string userId, DateTimeOffset at)
    {
        EnsureDraft();
        if (_lines.Count == 0) throw new DomainException("order_empty", "บิลยังไม่มีรายการ");
        if (BillDiscount > Subtotal) throw new DomainException("discount_exceeds", "ส่วนลดท้ายบิลเกินยอดรวม");

        var paid = payments.Sum(p => Quantity.RoundMoney(p.Amount));
        if (paid < Total)
            throw new DomainException("payment_insufficient", $"รับเงินไม่ครบ: ยอด {Total:N2} รับมา {paid:N2}");
        var change = paid - Total;
        var cash = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => Quantity.RoundMoney(p.Amount));
        if (change > cash)
            throw new DomainException("overpaid_non_cash", "จ่ายเกินยอดได้เฉพาะเงินสด (ทอนเงิน)");

        foreach (var p in payments)
            _payments.Add(new Payment(Id, p.Method, p.Amount, p.Reference, false, null, userId, at));
        ChangeAmount = change;
        Number = number;
        Status = SalesOrderStatus.Confirmed;
        ConfirmedAt = at;
        ConfirmedBy = userId;

        foreach (var l in _lines) l.MarkDeliveredWithoutStock();
        RefreshStatus();
        return _lines.Where(l => l.TracksStock).Select(l => new LineStockEffect(l, l.Qty, l.QtyBase)).ToList();
    }

    // ---------- รับของ ----------

    /// <summary>ลูกค้ามารับของ 1 รอบ — คืนยอดที่ต้องตัดสต็อก (จากของที่จองไว้)</summary>
    public (Fulfillment Doc, IReadOnlyList<LineStockEffect> Effects) Fulfil(string number, IReadOnlyList<LineQty> items,
        string userId, DateTimeOffset at, string? note = null)
    {
        if (Status is not (SalesOrderStatus.Confirmed or SalesOrderStatus.PartiallyFulfilled))
            throw new DomainException("order_state", $"บิลสถานะ {Status} รับของไม่ได้");
        if (items.Count == 0) throw new DomainException("fulfil_empty", "ต้องระบุรายการที่รับ");
        if (items.GroupBy(i => i.LineId).Any(g => g.Count() > 1))
            throw new DomainException("fulfil_duplicate_line", "ระบุบรรทัดซ้ำในรอบเดียว");

        var doc = new Fulfillment(Id, number, note, userId, at);
        var effects = new List<LineStockEffect>();
        foreach (var item in items)
        {
            var line = GetLine(item.LineId);
            var qtyBase = line.Fulfil(item.Qty);
            doc.Add(line, item.Qty, qtyBase);
            effects.Add(new LineStockEffect(line, item.Qty, qtyBase));
        }
        _fulfillments.Add(doc);
        RefreshStatus();
        return (doc, effects);
    }

    /// <summary>รับของที่เหลือทั้งหมด (ใช้ตอน "รับเลย")</summary>
    public (Fulfillment Doc, IReadOnlyList<LineStockEffect> Effects) FulfilAll(string number, string userId, DateTimeOffset at)
        => Fulfil(number, _lines.Where(l => l.TracksStock && l.RemainingQty > 0).Select(l => new LineQty(l.Id, l.RemainingQty)).ToList(),
            userId, at);

    public bool HasPendingPickup => _lines.Any(l => l.TracksStock && l.RemainingQty > 0);

    // ---------- ยกเลิก ----------

    /// <summary>
    /// ยกเลิกบิล: Draft ยกเลิกได้เลย; ยืนยันแล้วยกเลิกได้ถ้ายังไม่มีการรับของ → คืนเงินตามช่องทางเดิม
    /// คืนรายการที่ต้องปลดการจอง
    /// </summary>
    public IReadOnlyList<LineStockEffect> Cancel(string reason, string userId, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("reason_required", "ต้องระบุเหตุผลการยกเลิก");
        if (Status == SalesOrderStatus.Draft)
        {
            Status = SalesOrderStatus.Cancelled;
            CancelReason = reason.Trim();
            return [];
        }
        if (Status != SalesOrderStatus.Confirmed || _lines.Any(l => l.TracksStock && l.FulfilledQty > 0))
            throw new DomainException("cancel_after_pickup", "บิลที่รับของไปแล้วยกเลิกไม่ได้ ให้ทำรายการคืนของแทน");

        var toRelease = _lines.Where(l => l.TracksStock).Select(l => new LineStockEffect(l, l.Qty, l.QtyBase)).ToList();
        var cashBack = ChangeAmount;
        foreach (var p in _payments.Where(p => !p.IsRefund).ToList())
        {
            var amount = p.Amount;
            if (p.Method == PaymentMethod.Cash && cashBack > 0)
            {
                var take = Math.Min(cashBack, amount);
                amount -= take;
                cashBack -= take;
            }
            if (amount > 0) _payments.Add(new Payment(Id, p.Method, amount, p.Reference, true, null, userId, at));
        }
        Status = SalesOrderStatus.Cancelled;
        CancelReason = reason.Trim();
        return toRelease;
    }

    // ---------- คืนของ ----------

    /// <summary>คืนของที่รับไปแล้ว → คืนเงิน (ส่วนลดท้ายบิลเฉลี่ยตามสัดส่วน)</summary>
    public (SalesReturn Doc, IReadOnlyList<LineStockEffect> Effects) Return(string number, IReadOnlyList<LineQty> items,
        string reason, PaymentMethod refundMethod, string userId, DateTimeOffset at)
    {
        if (Status is not (SalesOrderStatus.PartiallyFulfilled or SalesOrderStatus.Closed))
            throw new DomainException("order_state", $"บิลสถานะ {Status} คืนของไม่ได้");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("reason_required", "ต้องระบุเหตุผลการคืน");
        if (items.Count == 0) throw new DomainException("return_empty", "ต้องระบุรายการที่คืน");
        if (items.GroupBy(i => i.LineId).Any(g => g.Count() > 1))
            throw new DomainException("return_duplicate_line", "ระบุบรรทัดซ้ำ");

        var doc = new SalesReturn(Id, number, reason.Trim(), userId, at);
        var effects = new List<LineStockEffect>();
        foreach (var item in items)
        {
            var line = GetLine(item.LineId);
            var (qtyBase, refund) = line.Return(item.Qty, AllocatedNet(line));
            doc.Add(line, item.Qty, qtyBase, refund);
            if (line.TracksStock) effects.Add(new LineStockEffect(line, item.Qty, qtyBase));
        }
        _returns.Add(doc);
        if (doc.RefundAmount > 0)
            _payments.Add(new Payment(Id, refundMethod, doc.RefundAmount, doc.Number, true, doc.Id, userId, at));
        return (doc, effects);
    }

    /// <summary>ยอดสุทธิของบรรทัดหลังเฉลี่ยส่วนลดท้ายบิล</summary>
    public decimal AllocatedNet(SalesLine line)
        => Subtotal == 0 ? 0 : Quantity.RoundMoney(line.LineTotal * Total / Subtotal);

    // ---------- internals ----------

    public SalesLine GetLine(Guid lineId)
        => _lines.FirstOrDefault(l => l.Id == lineId) ?? throw new DomainException("line_not_found", $"ไม่พบบรรทัด {lineId} ในบิลนี้");

    private void RefreshStatus()
    {
        if (Status is SalesOrderStatus.Draft or SalesOrderStatus.Cancelled) return;
        if (!HasPendingPickup) Status = SalesOrderStatus.Closed;
        else if (_lines.Any(l => l.TracksStock && l.FulfilledQty > 0)) Status = SalesOrderStatus.PartiallyFulfilled;
    }

    private (decimal Price, PriceSource Source) Price(Sku sku, string unit, decimal qty, decimal? price, PriceSource? source)
    {
        if (price is { } manual) return (manual, source ?? PriceSource.Manual);
        var rule = sku.ResolvePrice(unit, Tier, qty)
                   ?? throw new DomainException("price_missing", $"{sku.Code} หน่วย '{unit}' ยังไม่ได้ตั้งราคา — กรอกราคาเอง");
        return (rule.Price, PriceSource.Rule);
    }

    private int NextLineNo() => _lines.Count == 0 ? 1 : _lines.Max(l => l.LineNo) + 1;

    private void EnsureDraft()
    {
        if (Status != SalesOrderStatus.Draft) throw new DomainException("order_not_draft", "แก้ไขได้เฉพาะบิลที่ยังไม่ยืนยัน");
    }
}
