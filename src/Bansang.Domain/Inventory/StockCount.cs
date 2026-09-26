using Bansang.Domain.Catalog;
using Bansang.Domain.Common;

namespace Bansang.Domain.Inventory;

public enum StockCountStatus { Open, Submitted }

public enum CountLineStatus
{
    /// <summary>บันทึกยอดนับแล้ว รอส่ง</summary>
    Recorded,
    /// <summary>ยอดตรง ไม่ต้องปรับ</summary>
    Matched,
    /// <summary>ส่วนต่างไม่เกินเกณฑ์ ปรับยอดอัตโนมัติ</summary>
    AutoAdjusted,
    /// <summary>ส่วนต่างเกินเกณฑ์ รอเจ้าของอนุมัติ</summary>
    PendingApproval,
    Approved,
    Rejected,
}

/// <summary>รอบตรวจนับ = "ปิดยอดลิ้นชักตอนเย็น"</summary>
public class StockCount
{
    private readonly List<StockCountLine> _lines = [];

    public Guid Id { get; private set; }
    public Guid LocationId { get; private set; }
    public StockCountStatus Status { get; private set; }
    public decimal ThresholdPercent { get; private set; }
    public string? Note { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public IReadOnlyList<StockCountLine> Lines => _lines;

    private StockCount() { }

    public StockCount(Guid locationId, decimal thresholdPercent, string createdBy, DateTimeOffset at, string? note = null)
    {
        if (thresholdPercent < 0) throw new DomainException("threshold_negative", "เกณฑ์ส่วนต่างต้องไม่ติดลบ");
        Id = Guid.CreateVersion7();
        LocationId = locationId;
        ThresholdPercent = thresholdPercent;
        CreatedBy = createdBy;
        CreatedAt = at;
        Note = note;
        Status = StockCountStatus.Open;
    }

    /// <summary>บันทึกยอดนับจริง (นับซ้ำ = แทนที่ของเดิม) พร้อม snapshot ยอดในระบบ ณ เวลานับ</summary>
    public StockCountLine RecordLine(Guid skuId, StockForm form, decimal systemQtyBase, decimal countedQtyBase,
        string? countedUnit, decimal? countedInputQty, string userId, DateTimeOffset at)
    {
        if (Status != StockCountStatus.Open) throw new DomainException("count_closed", "รอบนับนี้ส่งแล้ว แก้ไม่ได้");
        if (countedQtyBase < 0) throw new DomainException("count_negative", "ยอดนับต้องไม่ติดลบ");
        _lines.RemoveAll(l => l.SkuId == skuId && l.Form == form);
        var line = new StockCountLine(Id, skuId, form, systemQtyBase, countedQtyBase, countedUnit, countedInputQty, userId, at);
        _lines.Add(line);
        return line;
    }

    /// <summary>ส่งรอบนับ: ประเมินแต่ละบรรทัดตามเกณฑ์ คืนบรรทัดที่ต้องปรับยอดอัตโนมัติ</summary>
    public IReadOnlyList<StockCountLine> Submit(DateTimeOffset at)
    {
        if (Status != StockCountStatus.Open) throw new DomainException("count_closed", "รอบนับนี้ส่งแล้ว");
        if (_lines.Count == 0) throw new DomainException("count_empty", "ยังไม่มีรายการนับ");
        Status = StockCountStatus.Submitted;
        SubmittedAt = at;
        var auto = new List<StockCountLine>();
        foreach (var line in _lines)
        {
            if (line.VarianceBase == 0) line.SetStatus(CountLineStatus.Matched);
            else if (Math.Abs(line.VariancePercent) <= ThresholdPercent)
            {
                line.SetStatus(CountLineStatus.AutoAdjusted);
                auto.Add(line);
            }
            else line.SetStatus(CountLineStatus.PendingApproval);
        }
        return auto;
    }
}

public class StockCountLine
{
    public const string AutoAdjustReason = "ส่วนต่างการแบ่งขาย";

    public Guid Id { get; private set; }
    public Guid CountId { get; private set; }
    public Guid SkuId { get; private set; }
    public StockForm Form { get; private set; }
    public decimal SystemQtyBase { get; private set; }
    public decimal CountedQtyBase { get; private set; }
    public string? CountedUnit { get; private set; }
    public decimal? CountedInputQty { get; private set; }
    public decimal VarianceBase { get; private set; }
    public decimal VariancePercent { get; private set; }
    public CountLineStatus Status { get; private set; }
    public string? Reason { get; private set; }
    public string CountedBy { get; private set; } = null!;
    public DateTimeOffset CountedAt { get; private set; }
    public string? ResolvedBy { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public Guid? AdjustmentMovementId { get; private set; }

    private StockCountLine() { }

    internal StockCountLine(Guid countId, Guid skuId, StockForm form, decimal systemQtyBase, decimal countedQtyBase,
        string? countedUnit, decimal? countedInputQty, string userId, DateTimeOffset at)
    {
        Id = Guid.CreateVersion7();
        CountId = countId;
        SkuId = skuId;
        Form = form;
        SystemQtyBase = Quantity.Round(systemQtyBase);
        CountedQtyBase = Quantity.Round(countedQtyBase);
        CountedUnit = countedUnit;
        CountedInputQty = countedInputQty;
        VarianceBase = CountedQtyBase - SystemQtyBase;
        VariancePercent = SystemQtyBase == 0
            ? (VarianceBase == 0 ? 0 : 100 * Math.Sign(VarianceBase))
            : Math.Round(VarianceBase / SystemQtyBase * 100, 2, MidpointRounding.AwayFromZero);
        Status = CountLineStatus.Recorded;
        CountedBy = userId;
        CountedAt = at;
    }

    internal void SetStatus(CountLineStatus status) => Status = status;

    public void MarkAdjusted(Guid movementId, string userId, DateTimeOffset at)
    {
        if (Status != CountLineStatus.AutoAdjusted)
            throw new DomainException("count_line_state", "บรรทัดนี้ไม่ได้อยู่ในสถานะปรับอัตโนมัติ");
        AdjustmentMovementId = movementId;
        Reason = AutoAdjustReason;
        ResolvedBy = userId;
        ResolvedAt = at;
    }

    public void Approve(string reason, Guid movementId, string userId, DateTimeOffset at)
    {
        EnsurePending();
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("reason_required", "ต้องใส่สาเหตุของส่วนต่าง");
        Status = CountLineStatus.Approved;
        Reason = reason.Trim();
        AdjustmentMovementId = movementId;
        ResolvedBy = userId;
        ResolvedAt = at;
    }

    public void Reject(string reason, string userId, DateTimeOffset at)
    {
        EnsurePending();
        Status = CountLineStatus.Rejected;
        Reason = reason;
        ResolvedBy = userId;
        ResolvedAt = at;
    }

    private void EnsurePending()
    {
        if (Status != CountLineStatus.PendingApproval)
            throw new DomainException("count_line_state", "บรรทัดนี้ไม่ได้รออนุมัติ");
    }
}
