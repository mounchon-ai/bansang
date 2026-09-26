using Bansang.Application.Abstractions;
using Bansang.Application.Catalog;
using Bansang.Application.Contracts;
using Bansang.Domain.Catalog;
using Bansang.Domain.Common;
using Bansang.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bansang.Application.Inventory;

/// <summary>ตรวจนับและเคลียร์ส่วนต่าง ("ปิดยอดลิ้นชักตอนเย็น")</summary>
public sealed class CountService(IAppDbContext db, StockService stock, ICurrentUser user, TimeProvider clock,
    IOptions<InventoryOptions> options)
{
    private readonly InventoryOptions _opt = options.Value;

    public async Task<CountDto> CreateAsync(CreateCountRequest r, CancellationToken ct)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == r.LocationId, ct)) throw new NotFoundException("ที่เก็บ", r.LocationId);
        var count = new StockCount(r.LocationId, r.ThresholdPercent ?? _opt.CountVarianceThresholdPercent, user.UserId,
            clock.GetUtcNow(), r.Note);
        db.StockCounts.Add(count);
        await db.SaveChangesAsync(ct);
        return count.ToDto();
    }

    public async Task<CountDto> GetAsync(Guid id, CancellationToken ct)
        => (await LoadAsync(id, ct, tracking: false)).ToDto();

    public async Task<PagedResult<CountListItemDto>> ListAsync(CountQuery q, CancellationToken ct)
    {
        var query = db.StockCounts.AsNoTracking();
        if (q.LocationId is { } loc) query = query.Where(c => c.LocationId == loc);
        if (q.Status is { } status) query = query.Where(c => c.Status == status);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(c => c.CreatedAt).Skip(q.Skip).Take(q.SafePageSize)
            .Select(c => new CountListItemDto(c.Id, c.LocationId, c.Status, c.CreatedAt, c.CreatedBy, c.Lines.Count,
                c.Lines.Count(l => l.Status == CountLineStatus.PendingApproval)))
            .ToListAsync(ct);
        return new(items, q.SafePage, q.SafePageSize, total);
    }

    /// <summary>บันทึกยอดนับจริง พร้อม snapshot ยอดในระบบ ณ ตอนนั้น</summary>
    public async Task<CountLineDto> RecordLineAsync(Guid countId, RecordCountLineRequest r, CancellationToken ct)
    {
        var count = await LoadAsync(countId, ct);
        var sku = await CatalogService.LoadSkuAsync(db, r.SkuId, ct, tracking: false);
        var unit = sku.GetUnit(r.Unit);
        if (r.Qty < 0) throw new DomainException("count_negative", "ยอดนับต้องไม่ติดลบ");
        var countedBase = r.Qty == 0 ? 0 : UnitConverter.ToBase(unit, r.Qty).QtyBase;
        var form = r.Form ?? unit.Form;
        var systemQty = await db.StockBuckets
            .Where(b => b.SkuId == sku.Id && b.LocationId == count.LocationId && b.Form == form)
            .Select(b => (decimal?)b.QtyOnHandBase).FirstOrDefaultAsync(ct) ?? 0m;

        var line = count.RecordLine(sku.Id, form, systemQty, countedBase, unit.UnitName, r.Qty, user.UserId, clock.GetUtcNow());
        db.StockCountLines.Add(line);
        await db.SaveChangesAsync(ct);
        return line.ToDto();
    }

    /// <summary>ส่งรอบนับ: ส่วนต่างไม่เกินเกณฑ์ปรับอัตโนมัติ เกินเกณฑ์รอเจ้าของอนุมัติ</summary>
    public async Task<CountDto> SubmitAsync(Guid countId, CancellationToken ct)
    {
        var skuIds = await db.StockCountLines.Where(l => l.CountId == countId).Select(l => l.SkuId).Distinct().ToListAsync(ct);
        return await stock.InLockAsync(skuIds, async () =>
        {
            var count = await LoadAsync(countId, ct);
            var now = clock.GetUtcNow();
            var auto = count.Submit(now);
            foreach (var line in auto)
            {
                var m = await stock.PostCountAdjustmentAsync(line.SkuId, count.LocationId, line.Form, line.VarianceBase,
                    StockCountLine.AutoAdjustReason, CountRef(count), ct);
                line.MarkAdjusted(m.Id, user.UserId, now);
            }
            foreach (var line in count.Lines.Where(l => l.Status == CountLineStatus.Matched))
                await ClearFlagAsync(line.SkuId, count.LocationId, line.Form, ct);
            return count.ToDto();
        }, ct);
    }

    public Task<CountLineDto> ApproveAsync(Guid countId, Guid lineId, ResolveCountLineRequest r, CancellationToken ct)
        => ResolveAsync(countId, lineId, async (count, line) =>
        {
            var m = await stock.PostCountAdjustmentAsync(line.SkuId, count.LocationId, line.Form, line.VarianceBase,
                r.Reason, CountRef(count), ct);
            line.Approve(r.Reason, m.Id, user.UserId, clock.GetUtcNow());
        }, ct);

    public Task<CountLineDto> RejectAsync(Guid countId, Guid lineId, ResolveCountLineRequest r, CancellationToken ct)
        => ResolveAsync(countId, lineId, (_, line) =>
        {
            line.Reject(r.Reason, user.UserId, clock.GetUtcNow());
            return Task.CompletedTask;
        }, ct);

    /// <summary>
    /// รายการที่ควรนับวันนี้: ของติดธงก่อน แล้วตามด้วยกองแบ่งขาย (Loose) ที่ไม่ได้นับนานที่สุด
    /// (ภายหลังให้ Hangfire เรียกทุกวันแล้วส่งเข้า LINE)
    /// </summary>
    public async Task<IReadOnlyList<CountSuggestionDto>> SuggestAsync(Guid locationId, int take, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 100);
        var lastCounted = await db.StockCountLines.AsNoTracking()
            .Join(db.StockCounts.Where(c => c.LocationId == locationId), l => l.CountId, c => c.Id, (l, c) => l)
            .GroupBy(l => new { l.SkuId, l.Form })
            .Select(g => new { g.Key.SkuId, g.Key.Form, At = g.Max(l => l.CountedAt) })
            .ToListAsync(ct);
        var lastMap = lastCounted.ToDictionary(x => (x.SkuId, x.Form), x => x.At);

        var candidates = await db.StockBuckets.AsNoTracking()
            .Where(b => b.LocationId == locationId && (b.NeedsCount || (b.Form == StockForm.Loose && b.QtyOnHandBase != 0)))
            .Join(db.Skus, b => b.SkuId, s => s.Id, (b, s) => new { Bucket = b, s.Code, s.Name })
            .ToListAsync(ct);

        return candidates
            .Select(x => new
            {
                x,
                Last = lastMap.TryGetValue((x.Bucket.SkuId, x.Bucket.Form), out var at) ? at : (DateTimeOffset?)null,
            })
            .OrderByDescending(x => x.x.Bucket.NeedsCount)
            .ThenBy(x => x.Last ?? DateTimeOffset.MinValue)
            .Take(take)
            .Select(x => new CountSuggestionDto(x.x.Bucket.SkuId, x.x.Code, x.x.Name, locationId, x.x.Bucket.Form,
                x.x.Bucket.QtyOnHandBase, x.x.Bucket.NeedsCount, x.Last,
                x.x.Bucket.NeedsCount ? "ติดธง (เคยขายติดลบ)" : x.Last is null ? "ยังไม่เคยนับ" : "ไม่ได้นับนานที่สุด"))
            .ToList();
    }

    /// <summary>สถิติส่วนต่างรายสินค้า + คำแนะนำแก้สูตรประมาณ ถ้าเพี้ยนทางเดียวกันซ้ำ ๆ</summary>
    public async Task<VarianceStatsDto> VarianceStatsAsync(Guid skuId, string? unit, CancellationToken ct)
    {
        var sku = await CatalogService.LoadSkuAsync(db, skuId, ct, tracking: false);
        var approxUnit = unit is null ? sku.Units.FirstOrDefault(u => !u.IsExact) : sku.GetUnit(unit);

        var history = await db.StockCountLines.AsNoTracking()
            .Where(l => l.SkuId == skuId && l.Form == StockForm.Loose
                        && (l.Status == CountLineStatus.Matched || l.Status == CountLineStatus.AutoAdjusted
                            || l.Status == CountLineStatus.Approved))
            .OrderBy(l => l.CountedAt)
            .ToListAsync(ct);

        FactorSuggestion? suggestion = null;
        var streak = _opt.FactorSuggestionStreak;
        var nonZero = history.Where(l => l.VarianceBase != 0).ToList();
        if (approxUnit is not null && nonZero.Count >= streak)
        {
            var streakLines = nonZero.TakeLast(streak).ToList();
            var firstIdx = history.IndexOf(streakLines[0]);
            var from = firstIdx > 0 ? history[firstIdx - 1].CountedAt : DateTimeOffset.MinValue;
            var to = streakLines[^1].CountedAt;
            var unitName = approxUnit.UnitName;
            var sales = await db.StockMovements.AsNoTracking()
                .Where(m => m.SkuId == skuId && m.Type == MovementType.Issue && m.IsApproximate && m.InputUnit == unitName
                            && m.At > from && m.At <= to)
                .GroupBy(_ => 1)
                .Select(g => new { Base = -g.Sum(m => m.QtyBase), Input = g.Sum(m => m.InputQty ?? 0) })
                .FirstOrDefaultAsync(ct);
            if (sales is not null)
                suggestion = FactorAdvisor.Suggest(streakLines.Select(l => l.VarianceBase).ToList(), sales.Base, sales.Input,
                    approxUnit.FactorToBase, streak);
        }

        return new VarianceStatsDto(skuId, approxUnit?.UnitName, history.Select(l => l.ToDto()).ToList(), suggestion);
    }

    // ---------- internals ----------

    private async Task<CountLineDto> ResolveAsync(Guid countId, Guid lineId, Func<StockCount, StockCountLine, Task> action,
        CancellationToken ct)
    {
        var skuId = await db.StockCountLines.Where(l => l.Id == lineId && l.CountId == countId).Select(l => (Guid?)l.SkuId)
                        .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("บรรทัดตรวจนับ", lineId);
        return await stock.InLockAsync([skuId], async () =>
        {
            var count = await LoadAsync(countId, ct);
            var line = count.Lines.Single(l => l.Id == lineId);
            await action(count, line);
            return line.ToDto();
        }, ct);
    }

    private async Task ClearFlagAsync(Guid skuId, Guid locationId, StockForm form, CancellationToken ct)
    {
        var bucket = await db.StockBuckets.FirstOrDefaultAsync(b => b.SkuId == skuId && b.LocationId == locationId && b.Form == form, ct);
        bucket?.ClearCountFlag();
    }

    private static string CountRef(StockCount c) => $"COUNT-{c.CreatedAt:yyyyMMdd}-{c.Id.ToString()[..8]}";

    private async Task<StockCount> LoadAsync(Guid id, CancellationToken ct, bool tracking = true)
    {
        var q = db.StockCounts.Include(c => c.Lines).AsQueryable();
        if (!tracking) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("รอบตรวจนับ", id);
    }
}
