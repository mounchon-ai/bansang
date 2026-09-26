using Bansang.Domain.Catalog;

namespace Bansang.Domain.Inventory;

public enum AvailabilityStatus
{
    /// <summary>ของในกองพอ</summary>
    Sufficient,
    /// <summary>กอง Loose ไม่พอ แต่แกะลังแล้วพอ — ระบบเสนอให้แกะ</summary>
    NeedsBreakBulk,
    /// <summary>ไม่พอ แต่ร้านอนุญาตขายติดลบ — ขายได้ + ติดธงให้ตรวจนับ</summary>
    AllowedNegative,
    /// <summary>ของหมด</summary>
    OutOfStock,
}

public sealed record AvailabilityPlan(
    AvailabilityStatus Status,
    StockForm Form,
    decimal RequiredBase,
    decimal AvailableBase,
    decimal ShortageBase,
    decimal SealedAvailableBase,
    string? BreakBulkUnit,
    int BoxesToOpen,
    bool IsApproximate)
{
    public bool CanFulfil => Status != AvailabilityStatus.OutOfStock;
}

/// <summary>ตัดสินใจตาม flowchart "ขายหน้าร้าน (มีหลายหน่วย)"</summary>
public static class AvailabilityPlanner
{
    public static AvailabilityPlan Plan(Sku sku, ConvertedQty request,
        decimal availableInFormBase, decimal sealedAvailableBase, bool allowNegative)
    {
        var required = request.QtyBase;
        var shortage = Math.Max(required - availableInFormBase, 0);

        AvailabilityPlan Result(AvailabilityStatus status, string? unit = null, int boxes = 0) =>
            new(status, request.Form, required, availableInFormBase, shortage, sealedAvailableBase, unit, boxes, request.IsApproximate);

        if (shortage == 0) return Result(AvailabilityStatus.Sufficient);

        // ขายยกลังแต่ลังไม่พอ → แจ้งของหมด (ไม่ขายลังติดลบ)
        if (request.Form == StockForm.Sealed) return Result(AvailabilityStatus.OutOfStock);

        var sealedUnit = sku.SmallestSealedUnit;
        if (sealedUnit is not null)
        {
            var boxesNeeded = (int)Math.Ceiling(shortage / sealedUnit.FactorToBase);
            var boxesAvailable = (int)Math.Floor(Math.Max(sealedAvailableBase, 0) / sealedUnit.FactorToBase);
            if (boxesNeeded <= boxesAvailable)
                return Result(AvailabilityStatus.NeedsBreakBulk, sealedUnit.UnitName, boxesNeeded);
            if (allowNegative)
                return Result(AvailabilityStatus.AllowedNegative, boxesAvailable > 0 ? sealedUnit.UnitName : null, boxesAvailable);
            return Result(AvailabilityStatus.OutOfStock);
        }

        return Result(allowNegative ? AvailabilityStatus.AllowedNegative : AvailabilityStatus.OutOfStock);
    }
}
