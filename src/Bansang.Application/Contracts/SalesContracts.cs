using Bansang.Domain.Catalog;
using Bansang.Domain.Inventory;
using Bansang.Domain.Sales;

namespace Bansang.Application.Contracts;

// ---------- ลูกค้า ----------

public record CustomerDto(Guid Id, string Code, string Name, string? Phone, string? TaxId, string? Address,
    CustomerTier Tier, bool IsActive);

/// <summary>Code ว่าง = ออกรหัสให้อัตโนมัติ (C00001)</summary>
public record SaveCustomerRequest(string Name, CustomerTier Tier = CustomerTier.Retail, string? Code = null,
    string? Phone = null, string? TaxId = null, string? Address = null, bool IsActive = true);

public record CustomerQuery(int Page = 1, int PageSize = 20, string? Search = null, CustomerTier? Tier = null,
    bool? IsActive = null) : PageQuery(Page, PageSize, Search);

// ---------- บิลขาย ----------

/// <summary>
/// 1 บรรทัดที่ POS ส่งมา — เลือกได้ 3 แบบ:
/// (1) Barcode (หน่วยตามบาร์โค้ด) (2) SkuId + Unit (3) ไม่มี SKU = สินค้าเบ็ดเตล็ด ต้องมี Description + UnitPrice
/// UnitPrice ว่าง = ใช้ตารางราคาตามระดับลูกค้า
/// </summary>
public record SalesLineInput(decimal Qty, Guid? SkuId = null, string? Unit = null, string? Barcode = null,
    decimal? UnitPrice = null, decimal Discount = 0, string? Description = null);

public record CreateSalesOrderRequest(Guid LocationId, FulfillmentMode Mode = FulfillmentMode.PickupNow,
    Guid? CustomerId = null, decimal BillDiscount = 0, string? Note = null, IReadOnlyList<SalesLineInput>? Lines = null);

public record UpdateSalesOrderRequest(Guid? CustomerId, FulfillmentMode Mode, decimal BillDiscount = 0, string? Note = null);

public record UpdateSalesLineRequest(decimal Qty, string? Unit = null, decimal? UnitPrice = null, decimal Discount = 0);

public record ConfirmSalesOrderRequest(IReadOnlyList<PaymentInput> Payments, bool AutoBreakBulk = true);

public record FulfilRequest(IReadOnlyList<LineQty> Lines, string? Note = null);

public record CancelSalesOrderRequest(string Reason);

public record ReturnRequest(IReadOnlyList<LineQty> Lines, string Reason, PaymentMethod RefundMethod = PaymentMethod.Cash);

public record SalesLineDto(Guid Id, int LineNo, Guid? SkuId, string? SkuCode, string Description, string UnitName,
    decimal Qty, decimal UnitPrice, PriceSource PriceSource, decimal LineDiscount, decimal LineTotal, bool TracksStock,
    decimal QtyBase, bool IsApproximate, StockForm? Form, decimal FulfilledQty, decimal RemainingQty, decimal ReturnedQty,
    AvailabilityPlan? Availability);

public record PaymentDto(Guid Id, PaymentMethod Method, decimal Amount, string? Reference, bool IsRefund, DateTimeOffset At, string UserId);

public record DocLineDto(Guid SalesLineId, decimal Qty, decimal QtyBase, decimal Amount = 0);

public record FulfillmentDto(Guid Id, string Number, DateTimeOffset At, string UserId, string? Note, IReadOnlyList<DocLineDto> Lines);

public record SalesReturnDto(Guid Id, string Number, DateTimeOffset At, string UserId, string Reason, decimal RefundAmount,
    IReadOnlyList<DocLineDto> Lines);

public record SalesOrderDto(
    Guid Id, string? Number, SalesOrderStatus Status, FulfillmentMode Mode, Guid? CustomerId, string? CustomerName,
    CustomerTier Tier, Guid LocationId, IReadOnlyList<SalesLineDto> Lines,
    decimal Subtotal, decimal BillDiscount, decimal Total, decimal VatRatePercent, decimal VatAmount, decimal TotalBeforeVat,
    decimal PaidAmount, decimal ChangeAmount, decimal RefundedAmount,
    IReadOnlyList<PaymentDto> Payments, IReadOnlyList<FulfillmentDto> Fulfillments, IReadOnlyList<SalesReturnDto> Returns,
    string? Note, string? CancelReason, Guid? QuotationId, string CreatedBy, DateTimeOffset CreatedAt,
    string? ConfirmedBy, DateTimeOffset? ConfirmedAt);

public record SalesOrderListItemDto(Guid Id, string? Number, SalesOrderStatus Status, FulfillmentMode Mode,
    Guid? CustomerId, string? CustomerName, decimal Total, DateTimeOffset CreatedAt, DateTimeOffset? ConfirmedAt, bool HasPendingPickup);

public record SalesQuery(int Page = 1, int PageSize = 20, string? Search = null, SalesOrderStatus? Status = null,
    Guid? CustomerId = null, DateTimeOffset? From = null, DateTimeOffset? To = null) : PageQuery(Page, PageSize, Search);

public record DepositLineDto(Guid LineId, string Description, string UnitName, decimal Qty, decimal RemainingQty);

public record DepositDto(Guid OrderId, string Number, Guid? CustomerId, string? CustomerName, FulfillmentMode Mode,
    DateTimeOffset ConfirmedAt, IReadOnlyList<DepositLineDto> Lines);

// ---------- ใบเสร็จ ----------

public record ReceiptStoreDto(string Name, string? Branch, string? Address, string? TaxId, string? Phone);

public record ReceiptLineDto(string Description, decimal Qty, string UnitName, decimal UnitPrice, decimal Discount, decimal Total, bool IsApproximate);

public record ReceiptDto(
    ReceiptStoreDto Store, string Title, string Number, DateTimeOffset IssuedAt, string Cashier,
    string? CustomerName, string? CustomerTaxId, string? CustomerAddress, string LocationName, FulfillmentMode Mode,
    IReadOnlyList<ReceiptLineDto> Lines, decimal Subtotal, decimal BillDiscount, decimal Total,
    decimal VatRatePercent, decimal VatAmount, decimal TotalBeforeVat,
    IReadOnlyList<PaymentDto> Payments, decimal ChangeAmount, IReadOnlyList<DepositLineDto> PendingPickup);

// ---------- ใบเสนอราคา ----------

public record CreateQuotationRequest(IReadOnlyList<SalesLineInput> Lines, Guid? CustomerId = null, DateOnly? ValidUntil = null,
    decimal BillDiscount = 0, string? Note = null);

public record ConvertQuotationRequest(Guid LocationId, FulfillmentMode Mode = FulfillmentMode.PickupNow);

public record QuotationLineDto(int LineNo, Guid? SkuId, string? SkuCode, string Description, string UnitName, decimal Qty,
    decimal UnitPrice, decimal LineDiscount, decimal LineTotal);

public record QuotationDto(Guid Id, string Number, QuotationStatus Status, Guid? CustomerId, string? CustomerName,
    CustomerTier Tier, DateOnly ValidUntil, IReadOnlyList<QuotationLineDto> Lines, decimal Subtotal, decimal BillDiscount,
    decimal Total, decimal VatAmount, string? Note, Guid? SalesOrderId, string CreatedBy, DateTimeOffset CreatedAt);

public record QuotationQuery(int Page = 1, int PageSize = 20, string? Search = null, QuotationStatus? Status = null,
    Guid? CustomerId = null) : PageQuery(Page, PageSize, Search);

public static class SalesMapping
{
    public static CustomerDto ToDto(this Customer c) => new(c.Id, c.Code, c.Name, c.Phone, c.TaxId, c.Address, c.Tier, c.IsActive);

    public static PaymentDto ToDto(this Payment p) => new(p.Id, p.Method, p.Amount, p.Reference, p.IsRefund, p.At, p.UserId);

    public static SalesOrderDto ToDto(this SalesOrder o, string? customerName,
        IReadOnlyDictionary<Guid, AvailabilityPlan>? availability = null) => new(
        o.Id, o.Number, o.Status, o.Mode, o.CustomerId, customerName, o.Tier, o.LocationId,
        o.Lines.OrderBy(l => l.LineNo).Select(l => new SalesLineDto(l.Id, l.LineNo, l.SkuId, l.SkuCode, l.Description,
            l.UnitName, l.Qty, l.UnitPrice, l.PriceSource, l.LineDiscount, l.LineTotal, l.TracksStock, l.QtyBase,
            l.IsApproximate, l.Form, l.FulfilledQty, l.RemainingQty, l.ReturnedQty,
            availability?.GetValueOrDefault(l.Id))).ToList(),
        o.Subtotal, o.BillDiscount, o.Total, o.VatRatePercent, o.VatAmount, o.TotalBeforeVat,
        o.PaidAmount, o.ChangeAmount, o.RefundedAmount,
        o.Payments.OrderBy(p => p.At).ThenBy(p => p.Id).Select(p => p.ToDto()).ToList(),
        o.Fulfillments.OrderBy(f => f.At).Select(f => new FulfillmentDto(f.Id, f.Number, f.At, f.UserId, f.Note,
            f.Lines.Select(x => new DocLineDto(x.SalesLineId, x.Qty, x.QtyBase)).ToList())).ToList(),
        o.Returns.OrderBy(r => r.At).Select(r => new SalesReturnDto(r.Id, r.Number, r.At, r.UserId, r.Reason, r.RefundAmount,
            r.Lines.Select(x => new DocLineDto(x.SalesLineId, x.Qty, x.QtyBase, x.Amount)).ToList())).ToList(),
        o.Note, o.CancelReason, o.QuotationId, o.CreatedBy, o.CreatedAt, o.ConfirmedBy, o.ConfirmedAt);

    public static IReadOnlyList<DepositLineDto> PendingLines(this SalesOrder o) => o.Lines
        .Where(l => l.TracksStock && l.RemainingQty > 0)
        .OrderBy(l => l.LineNo)
        .Select(l => new DepositLineDto(l.Id, l.Description, l.UnitName, l.Qty, l.RemainingQty))
        .ToList();

    public static QuotationDto ToDto(this Quotation q, string? customerName) => new(q.Id, q.Number, q.Status, q.CustomerId,
        customerName, q.Tier, q.ValidUntil,
        q.Lines.OrderBy(l => l.LineNo).Select(l => new QuotationLineDto(l.LineNo, l.SkuId, l.SkuCode, l.Description,
            l.UnitName, l.Qty, l.UnitPrice, l.LineDiscount, l.LineTotal)).ToList(),
        q.Subtotal, q.BillDiscount, q.Total, q.VatAmount, q.Note, q.SalesOrderId, q.CreatedBy, q.CreatedAt);
}
