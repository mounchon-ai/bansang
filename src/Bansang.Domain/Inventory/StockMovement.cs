namespace Bansang.Domain.Inventory;

/// <summary>ข้อมูลประกอบรายการเคลื่อนไหว — หน่วยที่ผู้ใช้กรอก, factor ณ วันนั้น, เอกสารอ้างอิง ฯลฯ</summary>
public sealed record MovementContext
{
    public string? InputUnit { get; init; }
    public decimal? InputQty { get; init; }
    public decimal? FactorSnapshot { get; init; }
    public bool IsApproximate { get; init; }
    public decimal CostPerBase { get; init; }
    public string? RefDocument { get; init; }
    public Guid? CorrelationId { get; init; }
    public string? Reason { get; init; }
    public string UserId { get; init; } = "system";
    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
    public Guid? ReversalOfId { get; init; }
}

/// <summary>
/// 1 บรรทัดในสมุดบัญชีสต็อก — immutable, แก้/ลบไม่ได้ ผิดให้ออกรายการกลับ
/// ยอดคงเหลือของกอง = ผลรวม QtyBase ของทุกบรรทัด
/// </summary>
public class StockMovement
{
    public Guid Id { get; private set; }
    public Guid BucketId { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid LocationId { get; private set; }
    public Domain.Catalog.StockForm Form { get; private set; }
    public MovementType Type { get; private set; }
    /// <summary>จำนวนในหน่วยฐาน มีเครื่องหมาย: + เข้า / − ออก</summary>
    public decimal QtyBase { get; private set; }
    public string? InputUnit { get; private set; }
    public decimal? InputQty { get; private set; }
    public decimal? FactorSnapshot { get; private set; }
    public bool IsApproximate { get; private set; }
    public decimal CostPerBase { get; private set; }
    public string? RefDocument { get; private set; }
    public Guid? CorrelationId { get; private set; }
    public string? Reason { get; private set; }
    public string UserId { get; private set; } = null!;
    public DateTimeOffset At { get; private set; }
    public Guid? ReversalOfId { get; private set; }
    /// <summary>ยอดคงเหลือของกองหลังรายการนี้ (ไว้ดูประวัติง่าย ๆ)</summary>
    public decimal BalanceAfterBase { get; private set; }

    private StockMovement() { }

    internal StockMovement(StockBucket bucket, MovementType type, decimal qtyBase, decimal balanceAfter, MovementContext ctx)
    {
        Id = Guid.CreateVersion7();
        BucketId = bucket.Id;
        SkuId = bucket.SkuId;
        LocationId = bucket.LocationId;
        Form = bucket.Form;
        Type = type;
        QtyBase = qtyBase;
        BalanceAfterBase = balanceAfter;
        InputUnit = ctx.InputUnit;
        InputQty = ctx.InputQty;
        FactorSnapshot = ctx.FactorSnapshot;
        IsApproximate = ctx.IsApproximate;
        CostPerBase = ctx.CostPerBase;
        RefDocument = ctx.RefDocument;
        CorrelationId = ctx.CorrelationId;
        Reason = ctx.Reason;
        UserId = ctx.UserId;
        At = ctx.At;
        ReversalOfId = ctx.ReversalOfId;
    }
}
