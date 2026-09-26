using Bansang.Application.Abstractions;
using Bansang.Application.Contracts;
using Bansang.Application.Inventory;
using Bansang.Domain.Catalog;
using Bansang.Domain.Common;
using Bansang.Domain.Inventory;
using Bansang.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bansang.Application.Sales;

/// <summary>
/// บิลขาย / POS — ขาย = จอง (Reserve), ของออกจริง = ตัด (Issue)
/// ทุกคำสั่งที่แตะสต็อก: ล็อกทุก SKU ในบิล (เรียงตาม id) → ล็อกแถวบิล → ทำงาน → save ใน transaction เดียว
/// </summary>
public sealed class SalesService(IAppDbContext db, StockService stock, DocumentNumbers numbers, ICurrentUser user,
    TimeProvider clock, IOptions<StoreOptions> store, IOptions<InventoryOptions> inventory)
{
    // ---------- Queries ----------

    public async Task<SalesOrderDto> GetAsync(Guid id, CancellationToken ct)
    {
        var order = await LoadAsync(id, ct, tracking: false);
        return await ToDtoAsync(order, ct);
    }

    public async Task<PagedResult<SalesOrderListItemDto>> ListAsync(SalesQuery q, CancellationToken ct)
    {
        var query = db.SalesOrders.AsNoTracking();
        if (q.Status is { } status) query = query.Where(o => o.Status == status);
        if (q.CustomerId is { } cid) query = query.Where(o => o.CustomerId == cid);
        if (q.From is { } from) query = query.Where(o => o.CreatedAt >= from);
        if (q.To is { } to) query = query.Where(o => o.CreatedAt < to);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            query = query.Where(o => EF.Functions.ILike(o.Number!, term)
                                     || db.Customers.Any(c => c.Id == o.CustomerId && EF.Functions.ILike(c.Name, term)));
        }

        var total = await query.CountAsync(ct);
        var page = await query.OrderByDescending(o => o.CreatedAt).Skip(q.Skip).Take(q.SafePageSize)
            .Include(o => o.Lines).AsSplitQuery().ToListAsync(ct);
        var names = await CustomerNamesAsync(page.Select(o => o.CustomerId), ct);
        var items = page.Select(o => new SalesOrderListItemDto(o.Id, o.Number, o.Status, o.Mode, o.CustomerId,
            o.CustomerId is { } c ? names.GetValueOrDefault(c) : null, o.Total, o.CreatedAt, o.ConfirmedAt,
            o.Status is SalesOrderStatus.Confirmed or SalesOrderStatus.PartiallyFulfilled && o.HasPendingPickup)).ToList();
        return new(items, q.SafePage, q.SafePageSize, total);
    }

    /// <summary>ของที่ลูกค้าซื้อฝากไว้ ยังรับไม่ครบ</summary>
    public async Task<IReadOnlyList<DepositDto>> DepositsAsync(Guid? customerId, Guid? locationId, CancellationToken ct)
    {
        var query = db.SalesOrders.AsNoTracking()
            .Where(o => o.Status == SalesOrderStatus.Confirmed || o.Status == SalesOrderStatus.PartiallyFulfilled);
        if (customerId is { } cid) query = query.Where(o => o.CustomerId == cid);
        if (locationId is { } lid) query = query.Where(o => o.LocationId == lid);
        var orders = await query.Include(o => o.Lines).OrderBy(o => o.ConfirmedAt).ToListAsync(ct);
        var names = await CustomerNamesAsync(orders.Select(o => o.CustomerId), ct);
        return orders.Where(o => o.HasPendingPickup)
            .Select(o => new DepositDto(o.Id, o.Number!, o.CustomerId,
                o.CustomerId is { } c ? names.GetValueOrDefault(c) : null, o.Mode, o.ConfirmedAt!.Value, o.PendingLines()))
            .ToList();
    }

    /// <summary>ข้อมูลใบเสร็จรับเงิน / ใบกำกับภาษีอย่างย่อ สำหรับพิมพ์</summary>
    public async Task<ReceiptDto> ReceiptAsync(Guid id, CancellationToken ct)
    {
        var o = await LoadAsync(id, ct, tracking: false);
        if (o.Number is null) throw new DomainException("order_not_confirmed", "บิลยังไม่ยืนยัน ยังไม่มีใบเสร็จ");
        var customer = o.CustomerId is { } cid ? await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid, ct) : null;
        var location = await db.Locations.AsNoTracking().Where(l => l.Id == o.LocationId).Select(l => l.Name).FirstAsync(ct);
        var s = store.Value;
        var title = o.VatRatePercent > 0 ? "ใบเสร็จรับเงิน / ใบกำกับภาษีอย่างย่อ" : "ใบเสร็จรับเงิน";
        return new ReceiptDto(
            new ReceiptStoreDto(s.Name, s.Branch, s.Address, s.TaxId, s.Phone), title, o.Number, o.ConfirmedAt!.Value,
            o.ConfirmedBy!, customer?.Name, customer?.TaxId, customer?.Address, location, o.Mode,
            o.Lines.OrderBy(l => l.LineNo).Select(l => new ReceiptLineDto(l.Description, l.Qty, l.UnitName, l.UnitPrice,
                l.LineDiscount, l.LineTotal, l.IsApproximate)).ToList(),
            o.Subtotal, o.BillDiscount, o.Total, o.VatRatePercent, o.VatAmount, o.TotalBeforeVat,
            o.Payments.Where(p => !p.IsRefund).OrderBy(p => p.At).Select(p => p.ToDto()).ToList(), o.ChangeAmount,
            o.PendingLines());
    }

    // ---------- Draft ----------

    public async Task<SalesOrderDto> CreateAsync(CreateSalesOrderRequest r, CancellationToken ct)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == r.LocationId && l.IsActive, ct))
            throw new NotFoundException("ที่เก็บ", r.LocationId);
        var tier = await TierAsync(r.CustomerId, ct);
        var order = new SalesOrder(r.LocationId, r.Mode, r.CustomerId, tier, store.Value.VatRatePercent, user.UserId,
            clock.GetUtcNow(), r.Note);
        order.Update(r.Mode, r.BillDiscount, r.Note);
        var resolver = new LineResolver(db);
        foreach (var input in r.Lines ?? [])
        {
            var (sku, unit) = await resolver.ResolveAsync(input, ct);
            resolver.AddTo(order, sku, unit, input);
        }
        db.SalesOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(order, ct);
    }

    public Task<SalesOrderDto> AddLineAsync(Guid id, SalesLineInput input, CancellationToken ct) => EditDraftAsync(id, async order =>
    {
        var resolver = new LineResolver(db);
        var (sku, unit) = await resolver.ResolveAsync(input, ct);
        resolver.AddTo(order, sku, unit, input);
    }, ct);

    public Task<SalesOrderDto> UpdateLineAsync(Guid id, Guid lineId, UpdateSalesLineRequest r, CancellationToken ct)
        => EditDraftAsync(id, async order =>
        {
            var line = order.GetLine(lineId);
            var sku = line.SkuId is { } skuId ? await new LineResolver(db).GetAsync(skuId, ct) : null;
            order.UpdateLine(lineId, sku, r.Unit ?? line.UnitName, r.Qty, r.UnitPrice, r.Discount);
        }, ct);

    public Task<SalesOrderDto> RemoveLineAsync(Guid id, Guid lineId, CancellationToken ct)
        => EditDraftAsync(id, order =>
        {
            order.RemoveLine(lineId);
            return Task.CompletedTask;
        }, ct);

    public Task<SalesOrderDto> UpdateAsync(Guid id, UpdateSalesOrderRequest r, CancellationToken ct) => EditDraftAsync(id, async order =>
    {
        if (r.CustomerId != order.CustomerId)
        {
            var tier = await TierAsync(r.CustomerId, ct);
            var resolver = new LineResolver(db);
            await resolver.LoadAllAsync(order.Lines.Where(l => l.SkuId is not null).Select(l => l.SkuId!.Value), ct);
            order.ChangeCustomer(r.CustomerId, tier, resolver.Skus);
        }
        order.Update(r.Mode, r.BillDiscount, r.Note);
    }, ct);

    // ---------- ยืนยัน / รับของ / ยกเลิก / คืน ----------

    /// <summary>
    /// ยืนยันบิล + รับเงิน: จองของทุกบรรทัด (แกะลังอัตโนมัติถ้าจำเป็น)
    /// ถ้า "รับเลย" จะตัดสต็อกทันทีใน transaction เดียวกัน → ปิดบิล
    /// </summary>
    public Task<SalesOrderDto> ConfirmAsync(Guid id, ConfirmSalesOrderRequest r, CancellationToken ct)
        => InOrderLockAsync(id, async order =>
        {
            var now = clock.GetUtcNow();
            var number = await numbers.NextAsync(DocumentNumbers.Invoice, ct);
            var toReserve = order.Confirm(number, r.Payments, user.UserId, now);
            foreach (var e in toReserve)
                await stock.ReserveForSaleAsync(e.Line, order.LocationId, number, r.AutoBreakBulk, ct);

            if (order.Mode == FulfillmentMode.PickupNow && order.HasPendingPickup)
            {
                var pickup = await numbers.NextAsync(DocumentNumbers.Pickup, ct);
                var (_, effects) = order.FulfilAll(pickup, user.UserId, now);
                await IssueAsync(order, effects, number, ct);
            }
        }, ct);

    /// <summary>ลูกค้ามารับของ (ฝากไว้ ทยอยรับ) หรือรถส่งของออก — ตัดจากของที่จองไว้</summary>
    public Task<SalesOrderDto> FulfilAsync(Guid id, FulfilRequest r, CancellationToken ct) => InOrderLockAsync(id, async order =>
    {
        var number = await numbers.NextAsync(DocumentNumbers.Pickup, ct);
        var (_, effects) = order.Fulfil(number, r.Lines, user.UserId, clock.GetUtcNow(), r.Note);
        await IssueAsync(order, effects, order.Number!, ct);
    }, ct);

    /// <summary>ยกเลิกบิล — ปลดการจองและบันทึกคืนเงิน (ต้องยังไม่มีการรับของ)</summary>
    public Task<SalesOrderDto> CancelAsync(Guid id, CancelSalesOrderRequest r, CancellationToken ct) => InOrderLockAsync(id, async order =>
    {
        var wasConfirmed = order.Status != SalesOrderStatus.Draft;
        var toRelease = order.Cancel(r.Reason, user.UserId, clock.GetUtcNow());
        if (!wasConfirmed) return;
        foreach (var e in toRelease)
            await stock.ReleaseReservedAsync(e.Line, order.LocationId, e.QtyBase, ct);
    }, ct);

    /// <summary>คืนของ — ของกลับเข้าสต็อก (movement Return) + คืนเงิน</summary>
    public Task<SalesOrderDto> ReturnAsync(Guid id, ReturnRequest r, CancellationToken ct) => InOrderLockAsync(id, async order =>
    {
        var number = await numbers.NextAsync(DocumentNumbers.Return, ct);
        var (_, effects) = order.Return(number, r.Lines, r.Reason, r.RefundMethod, user.UserId, clock.GetUtcNow());
        foreach (var e in effects)
            await stock.ReturnToStockAsync(e.Line, order.LocationId, e.Qty, e.QtyBase, number, r.Reason.Trim(), ct);
    }, ct);

    // ---------- internals ----------

    private async Task IssueAsync(SalesOrder order, IReadOnlyList<LineStockEffect> effects, string refDocument, CancellationToken ct)
    {
        foreach (var e in effects)
            await stock.IssueReservedAsync(e.Line, order.LocationId, e.Qty, e.QtyBase, refDocument, ct);
    }

    /// <summary>แก้บิล Draft: ล็อกแถวบิล กันสองเครื่องแก้/ยืนยันพร้อมกัน</summary>
    private async Task<SalesOrderDto> EditDraftAsync(Guid id, Func<SalesOrder, Task> edit, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockSalesOrderAsync(id, ct);
        var order = await LoadAsync(id, ct);
        await edit(order);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await ToDtoAsync(order, ct);
    }

    /// <summary>ล็อกทุก SKU ในบิล (ผ่าน StockService) แล้วล็อกแถวบิล — ลำดับเดียวกันทุกคำสั่ง กัน deadlock</summary>
    private async Task<SalesOrderDto> InOrderLockAsync(Guid id, Func<SalesOrder, Task> work, CancellationToken ct)
    {
        var skuIds = await db.SalesOrders.Where(o => o.Id == id)
            .SelectMany(o => o.Lines).Where(l => l.SkuId != null && l.TracksStock)
            .Select(l => l.SkuId!.Value).Distinct().ToListAsync(ct);
        if (skuIds.Count == 0 && !await db.SalesOrders.AnyAsync(o => o.Id == id, ct))
            throw new NotFoundException("บิลขาย", id);

        var order = await stock.InLockAsync(skuIds, async () =>
        {
            await db.LockSalesOrderAsync(id, ct);
            var o = await LoadAsync(id, ct);
            var locked = skuIds.ToHashSet();
            if (o.Lines.Any(l => l.TracksStock && !locked.Contains(l.SkuId!.Value)))
                throw new ConflictException("order_changed", "บิลถูกแก้ไขระหว่างทำรายการ กรุณาลองใหม่");
            await work(o);
            return o;
        }, ct);
        return await ToDtoAsync(order, ct);
    }

    private async Task<SalesOrder> LoadAsync(Guid id, CancellationToken ct, bool tracking = true)
    {
        var q = db.SalesOrders
            .Include(o => o.Lines)
            .Include(o => o.Payments)
            .Include(o => o.Fulfillments).ThenInclude(f => f.Lines)
            .Include(o => o.Returns).ThenInclude(r => r.Lines)
            .AsSplitQuery();
        if (!tracking) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException("บิลขาย", id);
    }

    private async Task<CustomerTier> TierAsync(Guid? customerId, CancellationToken ct)
    {
        if (customerId is not { } id) return CustomerTier.Retail;
        var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("ลูกค้า", id);
        if (!c.IsActive) throw new DomainException("customer_inactive", $"ลูกค้า {c.Name} ปิดใช้งานอยู่");
        return c.Tier;
    }

    private async Task<Dictionary<Guid, string>> CustomerNamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var list = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        if (list.Count == 0) return [];
        return await db.Customers.AsNoTracking().Where(c => list.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
    }

    private async Task<SalesOrderDto> ToDtoAsync(SalesOrder order, CancellationToken ct)
    {
        var name = order.CustomerId is { } cid ? (await CustomerNamesAsync([cid], ct)).GetValueOrDefault(cid) : null;
        var availability = order.Status == SalesOrderStatus.Draft ? await AvailabilityAsync(order, ct) : null;
        return order.ToDto(name, availability);
    }

    /// <summary>
    /// เตือนก่อนยืนยัน: ของพอไหม / ต้องแกะกี่ลัง — คิดสะสมเมื่อหลายบรรทัดใช้กองเดียวกัน
    /// (เป็นแค่คำเตือน ตอนยืนยันจะเช็คจริงภายใต้ล็อกอีกครั้ง)
    /// </summary>
    private async Task<Dictionary<Guid, AvailabilityPlan>> AvailabilityAsync(SalesOrder order, CancellationToken ct)
    {
        var lines = order.Lines.Where(l => l.TracksStock).OrderBy(l => l.LineNo).ToList();
        if (lines.Count == 0) return [];
        var resolver = new LineResolver(db);
        await resolver.LoadAllAsync(lines.Select(l => l.SkuId!.Value), ct);
        var skuIds = resolver.Skus.Keys.ToList();
        var buckets = await db.StockBuckets.AsNoTracking()
            .Where(b => b.LocationId == order.LocationId && skuIds.Contains(b.SkuId)).ToListAsync(ct);
        decimal Avail(Guid sku, StockForm f) => buckets.FirstOrDefault(b => b.SkuId == sku && b.Form == f)?.QtyAvailableBase ?? 0;

        var used = new Dictionary<(Guid, StockForm), decimal>();
        var result = new Dictionary<Guid, AvailabilityPlan>();
        foreach (var l in lines)
        {
            var key = (l.SkuId!.Value, l.Form!.Value);
            var cumulative = used.GetValueOrDefault(key) + l.QtyBase;
            used[key] = cumulative;
            var conv = new ConvertedQty(cumulative, l.UnitName, l.Qty, l.FactorSnapshot!.Value, l.IsApproximate, l.Form.Value);
            result[l.Id] = AvailabilityPlanner.Plan(resolver.Skus[key.Item1], conv, Avail(key.Item1, key.Item2),
                Avail(key.Item1, StockForm.Sealed), inventory.Value.AllowNegativeStock);
        }
        return result;
    }
}
