using Bansang.Domain.Catalog;
using Bansang.Domain.Inventory;

namespace Bansang.Application.Contracts;

public record LocationDto(Guid Id, string Code, string Name, bool IsActive);
public record CreateLocationRequest(string Code, string Name);
public record UpdateLocationRequest(string Name, bool IsActive);

/// <summary>รับของเข้า: UnitCost = ราคาต่อ 1 หน่วยที่รับ (เช่น 900 บาท/ลัง)</summary>
public record ReceiveRequest(Guid SkuId, Guid LocationId, string Unit, decimal Qty, decimal UnitCost, string? RefDocument, string? Note);

/// <summary>
/// ตัดสต็อก (ของออกจากร้าน)
/// AutoBreakBulk = กอง Loose ไม่พอแต่มีลัง → แกะให้อัตโนมัติในรายการเดียวกัน
/// FromReserved = ตัดจากของที่จองไว้ (ลูกค้ามารับของที่ซื้อฝากไว้)
/// </summary>
public record IssueRequest(Guid SkuId, Guid LocationId, string Unit, decimal Qty, string? RefDocument,
    bool AutoBreakBulk = true, bool FromReserved = false, string? Note = null);

public record ReserveRequest(Guid SkuId, Guid LocationId, string Unit, decimal Qty, string? RefDocument, bool AutoBreakBulk = true);

public record ReleaseRequest(Guid SkuId, Guid LocationId, string Unit, decimal Qty, string? RefDocument);

public record BreakBulkRequest(Guid SkuId, Guid LocationId, string? SealedUnit, int Count, string? RefDocument);

public record TransferRequest(Guid SkuId, Guid FromLocationId, Guid ToLocationId, string Unit, decimal Qty, string? RefDocument);

/// <summary>ปรับยอดด้วยมือ: Qty มีเครื่องหมาย (+ เพิ่ม / − ลด) ต้องใส่เหตุผลเสมอ</summary>
public record AdjustRequest(Guid SkuId, Guid LocationId, string Unit, decimal Qty, StockForm? Form, string Reason);

public record ReverseRequest(string Reason);

public record MovementDto(
    Guid Id, DateTimeOffset At, Guid SkuId, Guid LocationId, StockForm Form, MovementType Type, decimal QtyBase,
    decimal BalanceAfterBase, string? InputUnit, decimal? InputQty, decimal? FactorSnapshot, bool IsApproximate,
    decimal CostPerBase, string? RefDocument, Guid? CorrelationId, string? Reason, string UserId, Guid? ReversalOfId);

public record UnitQtyDto(string Unit, decimal Qty, bool IsApproximate);

public record BucketDto(
    Guid Id, Guid SkuId, string SkuCode, string SkuName, string BaseUnit, Guid LocationId, string LocationName,
    StockForm Form, decimal OnHandBase, decimal ReservedBase, decimal AvailableBase, bool NeedsCount,
    IReadOnlyList<UnitQtyDto> InUnits);

public record StockQuery(int Page = 1, int PageSize = 20, string? Search = null, Guid? SkuId = null,
    Guid? LocationId = null, StockForm? Form = null, bool? NeedsCount = null)
    : PageQuery(Page, PageSize, Search);

public record MovementQuery(int Page = 1, int PageSize = 50, string? Search = null, Guid? SkuId = null,
    Guid? LocationId = null, MovementType? Type = null, string? RefDocument = null,
    DateTimeOffset? From = null, DateTimeOffset? To = null)
    : PageQuery(Page, PageSize, Search);

public record StockOperationResult(IReadOnlyList<MovementDto> Movements, IReadOnlyList<BucketDto> Buckets,
    AvailabilityPlan? Plan = null);

public record ReconcileMismatchDto(Guid BucketId, Guid SkuId, Guid LocationId, StockForm Form, decimal CachedBase, decimal LedgerBase);

public static class InventoryMapping
{
    public static LocationDto ToDto(this Location l) => new(l.Id, l.Code, l.Name, l.IsActive);

    public static MovementDto ToDto(this StockMovement m) => new(m.Id, m.At, m.SkuId, m.LocationId, m.Form, m.Type,
        m.QtyBase, m.BalanceAfterBase, m.InputUnit, m.InputQty, m.FactorSnapshot, m.IsApproximate, m.CostPerBase,
        m.RefDocument, m.CorrelationId, m.Reason, m.UserId, m.ReversalOfId);

    /// <summary>ยอดของกองแสดงเป็นทุกหน่วยที่อยู่ในสภาพเดียวกัน (Sealed → ลัง, Loose → kg/ถุง/ตัว)</summary>
    public static BucketDto ToDto(this StockBucket b, Sku sku, Location location) => new(
        b.Id, sku.Id, sku.Code, sku.Name, sku.BaseUnit, location.Id, location.Name, b.Form,
        b.QtyOnHandBase, b.QtyReservedBase, b.QtyAvailableBase, b.NeedsCount,
        sku.Units.Where(u => u.Form == b.Form || u.IsBase)
            .OrderByDescending(u => u.FactorToBase)
            .Select(u => new UnitQtyDto(u.UnitName, UnitConverter.FromBase(u, b.QtyOnHandBase), !u.IsExact))
            .ToList());
}
