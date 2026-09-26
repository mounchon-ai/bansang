using Bansang.Domain.Catalog;
using Bansang.Domain.Inventory;

namespace Bansang.Application.Contracts;

public record CreateCountRequest(Guid LocationId, string? Note, decimal? ThresholdPercent);

/// <summary>บันทึกยอดนับจริง เช่น ชั่งได้ 18.4 kg หรือ นับได้ 3 ลัง (Form ไม่ระบุ = ตามหน่วย)</summary>
public record RecordCountLineRequest(Guid SkuId, string Unit, decimal Qty, StockForm? Form);

public record ResolveCountLineRequest(string Reason);

public record CountLineDto(Guid Id, Guid SkuId, StockForm Form, decimal SystemQtyBase, decimal CountedQtyBase,
    string? CountedUnit, decimal? CountedInputQty, decimal VarianceBase, decimal VariancePercent,
    CountLineStatus Status, string? Reason, string CountedBy, DateTimeOffset CountedAt,
    string? ResolvedBy, DateTimeOffset? ResolvedAt, Guid? AdjustmentMovementId);

public record CountDto(Guid Id, Guid LocationId, StockCountStatus Status, decimal ThresholdPercent, string? Note,
    string CreatedBy, DateTimeOffset CreatedAt, DateTimeOffset? SubmittedAt, IReadOnlyList<CountLineDto> Lines);

public record CountListItemDto(Guid Id, Guid LocationId, StockCountStatus Status, DateTimeOffset CreatedAt,
    string CreatedBy, int LineCount, int PendingCount);

public record CountQuery(int Page = 1, int PageSize = 20, Guid? LocationId = null, StockCountStatus? Status = null)
    : PageQuery(Page, PageSize);

public record CountSuggestionDto(Guid SkuId, string SkuCode, string SkuName, Guid LocationId, StockForm Form,
    decimal OnHandBase, bool NeedsCount, DateTimeOffset? LastCountedAt, string Why);

public record VarianceStatsDto(Guid SkuId, string? ApproxUnit, IReadOnlyList<CountLineDto> History,
    FactorSuggestion? Suggestion);

public static class CountMapping
{
    public static CountLineDto ToDto(this StockCountLine l) => new(l.Id, l.SkuId, l.Form, l.SystemQtyBase,
        l.CountedQtyBase, l.CountedUnit, l.CountedInputQty, l.VarianceBase, l.VariancePercent, l.Status, l.Reason,
        l.CountedBy, l.CountedAt, l.ResolvedBy, l.ResolvedAt, l.AdjustmentMovementId);

    public static CountDto ToDto(this StockCount c) => new(c.Id, c.LocationId, c.Status, c.ThresholdPercent, c.Note,
        c.CreatedBy, c.CreatedAt, c.SubmittedAt, c.Lines.OrderBy(l => l.CountedAt).Select(l => l.ToDto()).ToList());
}
