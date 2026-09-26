using Bansang.Application.Abstractions;
using Bansang.Application.Catalog;
using Bansang.Application.Contracts;
using Bansang.Domain.Catalog;
using Bansang.Domain.Common;
using Bansang.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bansang.Application.Inventory;

/// <summary>
/// ทุกการเปลี่ยนสต็อกผ่านที่นี่: ล็อก SKU → โหลดกอง → ลง ledger → save ใน transaction เดียว
/// </summary>
public sealed class StockService(IAppDbContext db, ICurrentUser user, TimeProvider clock, IOptions<InventoryOptions> options)
{
    private readonly InventoryOptions _opt = options.Value;

    // ---------- Queries ----------

    public async Task<AvailabilityPlan> CheckAvailabilityAsync(Guid skuId, Guid locationId, string unit, decimal qty, CancellationToken ct)
    {
        var sku = await CatalogService.LoadSkuAsync(db, skuId, ct, tracking: false);
        var conv = sku.ToBase(unit, qty);
        var buckets = await db.StockBuckets.AsNoTracking()
            .Where(b => b.SkuId == skuId && b.LocationId == locationId).ToListAsync(ct);
        decimal Avail(StockForm f) => buckets.FirstOrDefault(b => b.Form == f)?.QtyAvailableBase ?? 0;
        return AvailabilityPlanner.Plan(sku, conv, Avail(conv.Form), Avail(StockForm.Sealed), _opt.AllowNegativeStock);
    }

    public async Task<PagedResult<BucketDto>> ListStockAsync(StockQuery q, CancellationToken ct)
    {
        var query = db.StockBuckets.AsNoTracking();
        if (q.SkuId is { } skuId) query = query.Where(b => b.SkuId == skuId);
        if (q.LocationId is { } locId) query = query.Where(b => b.LocationId == locId);
        if (q.Form is { } form) query = query.Where(b => b.Form == form);
        if (q.NeedsCount is { } nc) query = query.Where(b => b.NeedsCount == nc);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            query = query.Where(b => db.Skus.Any(s => s.Id == b.SkuId
                                                     && (EF.Functions.ILike(s.Code, term) || EF.Functions.ILike(s.Name, term))));
        }

        var total = await query.CountAsync(ct);
        var page = await query
            .OrderBy(b => db.Skus.Where(s => s.Id == b.SkuId).Select(s => s.Code).First())
            .ThenBy(b => b.LocationId).ThenBy(b => b.Form)
            .Skip(q.Skip).Take(q.SafePageSize).ToListAsync(ct);
        return new(await ToDtosAsync(page, ct), q.SafePage, q.SafePageSize, total);
    }

    public async Task<PagedResult<MovementDto>> ListMovementsAsync(MovementQuery q, CancellationToken ct)
    {
        var query = db.StockMovements.AsNoTracking();
        if (q.SkuId is { } skuId) query = query.Where(m => m.SkuId == skuId);
        if (q.LocationId is { } locId) query = query.Where(m => m.LocationId == locId);
        if (q.Type is { } type) query = query.Where(m => m.Type == type);
        if (!string.IsNullOrWhiteSpace(q.RefDocument)) query = query.Where(m => m.RefDocument == q.RefDocument);
        if (q.From is { } from) query = query.Where(m => m.At >= from);
        if (q.To is { } to) query = query.Where(m => m.At < to);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            query = query.Where(m => EF.Functions.ILike(m.RefDocument!, term) || EF.Functions.ILike(m.Reason!, term)
                                     || EF.Functions.ILike(m.UserId, term));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(m => m.At).ThenByDescending(m => m.Id)
            .Skip(q.Skip).Take(q.SafePageSize).ToListAsync(ct);
        return new(items.Select(m => m.ToDto()).ToList(), q.SafePage, q.SafePageSize, total);
    }

    /// <summary>ตรวจว่ายอด cache ในกอง = ผลรวม ledger (ควรได้ list ว่างเสมอ)</summary>
    public async Task<IReadOnlyList<ReconcileMismatchDto>> ReconcileAsync(CancellationToken ct)
        => await db.StockBuckets.AsNoTracking()
            .Select(b => new
            {
                Bucket = b,
                Ledger = db.StockMovements.Where(m => m.BucketId == b.Id).Sum(m => (decimal?)m.QtyBase) ?? 0m,
            })
            .Where(x => x.Bucket.QtyOnHandBase != x.Ledger)
            .Select(x => new ReconcileMismatchDto(x.Bucket.Id, x.Bucket.SkuId, x.Bucket.LocationId, x.Bucket.Form,
                x.Bucket.QtyOnHandBase, x.Ledger))
            .ToListAsync(ct);

    // ---------- Commands ----------

    /// <summary>รับของเข้า: แปลงเป็นหน่วยฐาน ลงกองตามสภาพของหน่วย แล้วอัปเดตต้นทุนเฉลี่ย</summary>
    public Task<StockOperationResult> ReceiveAsync(ReceiveRequest r, CancellationToken ct) => InLockAsync([r.SkuId], async () =>
    {
        if (r.UnitCost < 0) throw new DomainException("cost_negative", "ต้นทุนต้องไม่ติดลบ");
        var scope = await LoadScopeAsync(r.SkuId, [r.LocationId], ct);
        var unit = scope.Sku.GetUnit(r.Unit);
        if (!unit.CanBuy) throw new DomainException("unit_not_buyable", $"หน่วย '{unit.UnitName}' ไม่ได้ตั้งให้ใช้รับของ");
        var conv = UnitConverter.ToBase(unit, r.Qty);
        var costPerBase = Quantity.RoundCost(r.UnitCost / unit.FactorToBase);

        var onHandBefore = await db.StockBuckets.Where(b => b.SkuId == r.SkuId).SumAsync(b => b.QtyOnHandBase, ct);
        scope.Sku.ApplyReceiptCost(onHandBefore, conv.QtyBase, costPerBase);

        var m = Post(scope.Bucket(r.LocationId, conv.Form), MovementType.Receive, conv.QtyBase,
            Ctx(conv, r.RefDocument, r.Note) with { CostPerBase = costPerBase });
        return Result(scope, [m]);
    }, ct);

    /// <summary>
    /// ตัดสต็อกจริง (ของออกจากร้าน) — ถ้ากอง Loose ไม่พอและมีลัง จะแกะลังให้ใน transaction เดียวกัน
    /// </summary>
    public Task<StockOperationResult> IssueAsync(IssueRequest r, CancellationToken ct) => InLockAsync([r.SkuId], async () =>
    {
        var scope = await LoadScopeAsync(r.SkuId, [r.LocationId], ct);
        var unit = scope.Sku.GetUnit(r.Unit);
        if (!unit.CanSell) throw new DomainException("unit_not_sellable", $"หน่วย '{unit.UnitName}' ไม่ได้ตั้งให้ขาย");
        var conv = UnitConverter.ToBase(unit, r.Qty);
        var movements = new List<StockMovement>();
        var ctx = Ctx(conv, r.RefDocument, r.Note) with { CostPerBase = scope.Sku.AvgCostPerBase };
        var bucket = scope.Bucket(r.LocationId, conv.Form);

        if (r.FromReserved)
        {
            movements.Add(Post(bucket, MovementType.Issue, -conv.QtyBase, ctx, consumeReserved: true));
            return Result(scope, movements);
        }

        var (plan, allowNegative) = PrepareAvailability(scope, r.LocationId, conv, r.AutoBreakBulk, r.RefDocument, movements);
        movements.Add(Post(bucket, MovementType.Issue, -conv.QtyBase, ctx, allowNegative));
        return Result(scope, movements, plan);
    }, ct);

    /// <summary>จองของ (ขายแล้ว ลูกค้ายังไม่รับ) — ถ้าจำเป็นจะแกะลังเหมือนตอนตัดสต็อก</summary>
    public Task<StockOperationResult> ReserveAsync(ReserveRequest r, CancellationToken ct) => InLockAsync([r.SkuId], async () =>
    {
        var scope = await LoadScopeAsync(r.SkuId, [r.LocationId], ct);
        var unit = scope.Sku.GetUnit(r.Unit);
        if (!unit.CanSell) throw new DomainException("unit_not_sellable", $"หน่วย '{unit.UnitName}' ไม่ได้ตั้งให้ขาย");
        var conv = UnitConverter.ToBase(unit, r.Qty);
        var movements = new List<StockMovement>();
        var (plan, allowNegative) = PrepareAvailability(scope, r.LocationId, conv, r.AutoBreakBulk, r.RefDocument, movements);
        scope.Bucket(r.LocationId, conv.Form).Reserve(conv.QtyBase, allowNegative);
        return Result(scope, movements, plan);
    }, ct);

    public Task<StockOperationResult> ReleaseAsync(ReleaseRequest r, CancellationToken ct) => InLockAsync([r.SkuId], async () =>
    {
        var scope = await LoadScopeAsync(r.SkuId, [r.LocationId], ct);
        var conv = scope.Sku.ToBase(r.Unit, r.Qty);
        scope.Bucket(r.LocationId, conv.Form).Release(conv.QtyBase);
        return Result(scope, []);
    }, ct);

    public Task<StockOperationResult> BreakBulkAsync(BreakBulkRequest r, CancellationToken ct) => InLockAsync([r.SkuId], async () =>
    {
        var scope = await LoadScopeAsync(r.SkuId, [r.LocationId], ct);
        var unit = r.SealedUnit is null
            ? scope.Sku.SmallestSealedUnit ?? throw new DomainException("no_sealed_unit", "SKU นี้ไม่มีหน่วยปิดผนึกให้แกะ")
            : scope.Sku.GetUnit(r.SealedUnit);
        var result = DoBreakBulk(scope, r.LocationId, unit, r.Count, r.RefDocument);
        return Result(scope, [result.Out, result.In]);
    }, ct);

    public Task<StockOperationResult> TransferAsync(TransferRequest r, CancellationToken ct) => InLockAsync([r.SkuId], async () =>
    {
        if (r.FromLocationId == r.ToLocationId) throw new DomainException("transfer_same_location", "ต้นทางกับปลายทางต้องต่างกัน");
        var scope = await LoadScopeAsync(r.SkuId, [r.FromLocationId, r.ToLocationId], ct);
        var conv = scope.Sku.ToBase(r.Unit, r.Qty);
        var ctx = Ctx(conv, r.RefDocument, null) with
        {
            CorrelationId = Guid.CreateVersion7(),
            CostPerBase = scope.Sku.AvgCostPerBase,
        };
        var @out = Post(scope.Bucket(r.FromLocationId, conv.Form), MovementType.TransferOut, -conv.QtyBase, ctx);
        var @in = Post(scope.Bucket(r.ToLocationId, conv.Form), MovementType.TransferIn, conv.QtyBase, ctx);
        return Result(scope, [@out, @in]);
    }, ct);

    /// <summary>ปรับยอดด้วยมือ — ต้องมีเหตุผล (audit trail)</summary>
    public Task<StockOperationResult> AdjustAsync(AdjustRequest r, CancellationToken ct) => InLockAsync([r.SkuId], async () =>
    {
        if (string.IsNullOrWhiteSpace(r.Reason)) throw new DomainException("reason_required", "ปรับยอดต้องใส่เหตุผล");
        if (r.Qty == 0) throw new DomainException("qty_zero", "จำนวนปรับต้องไม่เป็น 0");
        var scope = await LoadScopeAsync(r.SkuId, [r.LocationId], ct);
        var conv = scope.Sku.ToBase(r.Unit, Math.Abs(r.Qty));
        var form = r.Form ?? conv.Form;
        var m = PostAdjustment(scope, r.LocationId, form, Math.Sign(r.Qty) * conv.QtyBase, r.Reason,
            Ctx(conv, null, r.Reason));
        return Result(scope, [m]);
    }, ct);

    /// <summary>
    /// ออกรายการกลับ (ledger แก้ไม่ได้) — ถ้าเป็นแกะลัง/โอน จะกลับทั้งคู่
    /// หมายเหตุ: กลับรายการรับของไม่คำนวณต้นทุนเฉลี่ยย้อนหลัง
    /// </summary>
    public async Task<StockOperationResult> ReverseAsync(Guid movementId, ReverseRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Reason)) throw new DomainException("reason_required", "กลับรายการต้องใส่เหตุผล");
        var original = await db.StockMovements.AsNoTracking().FirstOrDefaultAsync(m => m.Id == movementId, ct)
                       ?? throw new NotFoundException("รายการเคลื่อนไหว", movementId);

        return await InLockAsync([original.SkuId], async () =>
        {
            if (original.Type == MovementType.Reversal)
                throw new DomainException("reverse_reversal", "รายการนี้เป็นรายการกลับอยู่แล้ว");

            var paired = original.CorrelationId is not null && original.Type is MovementType.BreakBulkIn or MovementType.BreakBulkOut
                or MovementType.TransferIn or MovementType.TransferOut;
            var group = paired
                ? await db.StockMovements.AsNoTracking().Where(m => m.CorrelationId == original.CorrelationId).ToListAsync(ct)
                : [original];
            var ids = group.Select(m => m.Id).ToList();
            if (await db.StockMovements.AnyAsync(m => m.ReversalOfId != null && ids.Contains(m.ReversalOfId.Value), ct))
                throw new ConflictException("already_reversed", "รายการนี้ถูกกลับไปแล้ว");

            var scope = await LoadScopeAsync(original.SkuId, group.Select(m => m.LocationId).Distinct(), ct);
            var correlation = paired ? Guid.CreateVersion7() : (Guid?)null;
            var movements = new List<StockMovement>();
            // คืนฝั่งที่เพิ่มของก่อน (เช่น คืน Sealed) แล้วค่อยหักอีกฝั่ง
            foreach (var m in group.OrderBy(m => m.QtyBase))
            {
                var ctx = new MovementContext
                {
                    InputUnit = m.InputUnit,
                    InputQty = m.InputQty,
                    FactorSnapshot = m.FactorSnapshot,
                    IsApproximate = m.IsApproximate,
                    CostPerBase = m.CostPerBase,
                    RefDocument = m.RefDocument,
                    CorrelationId = correlation,
                    Reason = r.Reason.Trim(),
                    UserId = user.UserId,
                    At = clock.GetUtcNow(),
                    ReversalOfId = m.Id,
                };
                movements.Add(Post(scope.Bucket(m.LocationId, m.Form), MovementType.Reversal, -m.QtyBase, ctx));
            }
            return Result(scope, movements);
        }, ct);
    }

    // ---------- ใช้ร่วมกับ CountService ----------

    internal async Task<StockMovement> PostCountAdjustmentAsync(Guid skuId, Guid locationId, StockForm form,
        decimal deltaBase, string reason, string refDocument, CancellationToken ct)
    {
        var scope = await LoadScopeAsync(skuId, [locationId], ct);
        var ctx = new MovementContext
        {
            RefDocument = refDocument,
            Reason = reason,
            UserId = user.UserId,
            At = clock.GetUtcNow(),
            CostPerBase = scope.Sku.AvgCostPerBase,
        };
        var m = PostAdjustment(scope, locationId, form, deltaBase, reason, ctx);
        scope.Bucket(locationId, form).ClearCountFlag();
        return m;
    }

    /// <summary>รันงานภายใต้ล็อก SKU — ถ้ามี transaction อยู่แล้ว (เรียกซ้อน) ใช้ตัวเดิม</summary>
    internal async Task<T> InLockAsync<T>(IEnumerable<Guid> skuIds, Func<Task<T>> work, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            await db.LockSkusAsync(skuIds, ct);
            return await work();
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockSkusAsync(skuIds, ct);
        var result = await work();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    // ---------- internals ----------

    private sealed class StockScope(IAppDbContext db, Sku sku, Dictionary<Guid, Location> locations, List<StockBucket> buckets)
    {
        public Sku Sku { get; } = sku;
        public Dictionary<Guid, Location> Locations { get; } = locations;
        public List<StockBucket> Buckets { get; } = buckets;

        public StockBucket Bucket(Guid locationId, StockForm form)
        {
            if (!Locations.ContainsKey(locationId)) throw new NotFoundException("ที่เก็บ", locationId);
            var b = Buckets.FirstOrDefault(x => x.LocationId == locationId && x.Form == form);
            if (b is not null) return b;
            b = new StockBucket(Sku.Id, locationId, form);
            db.StockBuckets.Add(b);
            Buckets.Add(b);
            return b;
        }
    }

    private async Task<StockScope> LoadScopeAsync(Guid skuId, IEnumerable<Guid> locationIds, CancellationToken ct)
    {
        var sku = await CatalogService.LoadSkuAsync(db, skuId, ct);
        if (!sku.IsStocked) throw new DomainException("sku_not_stocked", $"{sku.Code} เป็นสินค้าสั่งทำ ไม่เก็บสต็อก");
        var locIds = locationIds.Distinct().ToList();
        var locations = await db.Locations.Where(l => locIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var missing = locIds.FirstOrDefault(id => !locations.ContainsKey(id));
        if (missing != Guid.Empty) throw new NotFoundException("ที่เก็บ", missing);
        var inactive = locations.Values.FirstOrDefault(l => !l.IsActive);
        if (inactive is not null) throw new DomainException("location_inactive", $"ที่เก็บ '{inactive.Name}' ปิดใช้งานอยู่");
        var buckets = await db.StockBuckets.Where(b => b.SkuId == skuId && locIds.Contains(b.LocationId)).ToListAsync(ct);
        return new StockScope(db, sku, locations, buckets);
    }

    /// <summary>ตาม flowchart ขายหน้าร้าน: พอ / แกะลัง / ขายติดลบ / ของหมด</summary>
    private (AvailabilityPlan Plan, bool AllowNegative) PrepareAvailability(StockScope scope, Guid locationId,
        ConvertedQty conv, bool autoBreakBulk, string? refDocument, List<StockMovement> movements)
    {
        var target = scope.Bucket(locationId, conv.Form);
        var sealedBucket = scope.Bucket(locationId, StockForm.Sealed);
        var plan = AvailabilityPlanner.Plan(scope.Sku, conv, target.QtyAvailableBase, sealedBucket.QtyAvailableBase,
            _opt.AllowNegativeStock);

        switch (plan.Status)
        {
            case AvailabilityStatus.Sufficient:
                return (plan, false);
            case AvailabilityStatus.NeedsBreakBulk when !autoBreakBulk:
                throw new InsufficientStockException(
                    $"กอง Loose ไม่พอ (ขาด {plan.ShortageBase} {scope.Sku.BaseUnit}) — ต้องแกะ {plan.BoxesToOpen} {plan.BreakBulkUnit}",
                    plan.ShortageBase);
            case AvailabilityStatus.NeedsBreakBulk:
            case AvailabilityStatus.AllowedNegative:
                if (plan.BoxesToOpen > 0 && autoBreakBulk)
                {
                    var r = DoBreakBulk(scope, locationId, scope.Sku.GetUnit(plan.BreakBulkUnit!), plan.BoxesToOpen, refDocument);
                    movements.Add(r.Out);
                    movements.Add(r.In);
                }
                return (plan, plan.Status == AvailabilityStatus.AllowedNegative);
            default:
                throw new InsufficientStockException(
                    $"{scope.Sku.Name} ไม่พอ: ต้องการ {plan.RequiredBase} {scope.Sku.BaseUnit} มี {plan.AvailableBase}",
                    plan.ShortageBase);
        }
    }

    private BreakBulkResult DoBreakBulk(StockScope scope, Guid locationId, SkuUnit sealedUnit, decimal count, string? refDocument)
    {
        var ctx = new MovementContext { RefDocument = refDocument, UserId = user.UserId, At = clock.GetUtcNow() };
        var r = BreakBulk.Execute(scope.Sku, sealedUnit, count,
            scope.Bucket(locationId, StockForm.Sealed), scope.Bucket(locationId, StockForm.Loose), ctx);
        db.StockMovements.Add(r.Out);
        db.StockMovements.Add(r.In);
        return r;
    }

    private StockMovement PostAdjustment(StockScope scope, Guid locationId, StockForm form, decimal deltaBase,
        string reason, MovementContext ctx)
        => Post(scope.Bucket(locationId, form), MovementType.Adjust, deltaBase,
            ctx with { Reason = reason.Trim(), CostPerBase = scope.Sku.AvgCostPerBase }, allowNegative: true);

    private StockMovement Post(StockBucket bucket, MovementType type, decimal qtyBase, MovementContext ctx,
        bool allowNegative = false, bool consumeReserved = false)
    {
        var m = bucket.Post(type, qtyBase, ctx, allowNegative, consumeReserved);
        db.StockMovements.Add(m);
        return m;
    }

    private MovementContext Ctx(ConvertedQty conv, string? refDocument, string? reason) => new()
    {
        InputUnit = conv.InputUnit,
        InputQty = conv.InputQty,
        FactorSnapshot = conv.FactorSnapshot,
        IsApproximate = conv.IsApproximate,
        RefDocument = string.IsNullOrWhiteSpace(refDocument) ? null : refDocument.Trim(),
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
        UserId = user.UserId,
        At = clock.GetUtcNow(),
    };

    private static StockOperationResult Result(StockScope scope, IReadOnlyList<StockMovement> movements,
        AvailabilityPlan? plan = null)
    {
        var buckets = scope.Buckets
            .OrderBy(b => b.LocationId).ThenBy(b => b.Form)
            .Select(b => b.ToDto(scope.Sku, scope.Locations[b.LocationId]))
            .ToList();
        return new StockOperationResult(movements.Select(m => m.ToDto()).ToList(), buckets, plan);
    }

    private async Task<IReadOnlyList<BucketDto>> ToDtosAsync(IReadOnlyList<StockBucket> buckets, CancellationToken ct)
    {
        var skuIds = buckets.Select(b => b.SkuId).Distinct().ToList();
        var locIds = buckets.Select(b => b.LocationId).Distinct().ToList();
        var skus = await db.Skus.AsNoTracking().Include(s => s.Units).Where(s => skuIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        var locs = await db.Locations.AsNoTracking().Where(l => locIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        return buckets.Select(b => b.ToDto(skus[b.SkuId], locs[b.LocationId])).ToList();
    }
}
