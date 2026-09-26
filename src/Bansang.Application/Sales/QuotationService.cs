using Bansang.Application.Abstractions;
using Bansang.Application.Contracts;
using Bansang.Domain.Catalog;
using Bansang.Domain.Common;
using Bansang.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bansang.Application.Sales;

public sealed class QuotationService(IAppDbContext db, DocumentNumbers numbers, SalesService sales, ICurrentUser user,
    TimeProvider clock, IOptions<StoreOptions> store)
{
    public async Task<QuotationDto> CreateAsync(CreateQuotationRequest r, CancellationToken ct)
    {
        var tier = CustomerTier.Retail;
        string? customerName = null;
        if (r.CustomerId is { } cid)
        {
            var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cid, ct) ?? throw new NotFoundException("ลูกค้า", cid);
            tier = c.Tier;
            customerName = c.Name;
        }
        var validUntil = r.ValidUntil ?? numbers.Today.AddDays(store.Value.QuotationValidDays);
        if (validUntil < numbers.Today) throw new DomainException("valid_until_past", "วันหมดอายุต้องไม่อยู่ในอดีต");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var q = new Quotation(await numbers.NextAsync(DocumentNumbers.Quotation, ct), r.CustomerId, tier, validUntil,
            r.BillDiscount, store.Value.VatRatePercent, user.UserId, clock.GetUtcNow(), r.Note);
        var resolver = new LineResolver(db);
        foreach (var input in r.Lines)
        {
            var (sku, unit) = await resolver.ResolveAsync(input, ct);
            if (sku is null) q.AddMiscLine(input.Description!, unit, input.Qty, input.UnitPrice!.Value, input.Discount);
            else q.AddLine(sku, unit, input.Qty, input.UnitPrice, input.Discount);
        }
        if (q.BillDiscount > q.Subtotal) throw new DomainException("discount_exceeds", "ส่วนลดท้ายบิลเกินยอดรวม");
        db.Quotations.Add(q);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return q.ToDto(customerName);
    }

    public async Task<QuotationDto> GetAsync(Guid id, CancellationToken ct)
    {
        var q = await LoadAsync(id, ct, tracking: false);
        return q.ToDto(await CustomerNameAsync(q.CustomerId, ct));
    }

    public async Task<PagedResult<QuotationDto>> ListAsync(QuotationQuery query, CancellationToken ct)
    {
        var qs = db.Quotations.AsNoTracking();
        if (query.Status is { } s) qs = qs.Where(q => q.Status == s);
        if (query.CustomerId is { } cid) qs = qs.Where(q => q.CustomerId == cid);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";
            qs = qs.Where(q => EF.Functions.ILike(q.Number, term)
                               || db.Customers.Any(c => c.Id == q.CustomerId && EF.Functions.ILike(c.Name, term)));
        }
        var total = await qs.CountAsync(ct);
        var page = await qs.OrderByDescending(q => q.CreatedAt).Skip(query.Skip).Take(query.SafePageSize)
            .Include(q => q.Lines).AsSplitQuery().ToListAsync(ct);
        var ids = page.Where(q => q.CustomerId != null).Select(q => q.CustomerId!.Value).Distinct().ToList();
        var names = await db.Customers.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        return new(page.Select(q => q.ToDto(q.CustomerId is { } c ? names.GetValueOrDefault(c) : null)).ToList(),
            query.SafePage, query.SafePageSize, total);
    }

    /// <summary>แปลงใบเสนอราคาเป็นบิลขาย (Draft) โดยคงราคาที่เสนอไว้</summary>
    public async Task<SalesOrderDto> ConvertAsync(Guid id, ConvertQuotationRequest r, CancellationToken ct)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == r.LocationId && l.IsActive, ct))
            throw new NotFoundException("ที่เก็บ", r.LocationId);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var q = await LoadAsync(id, ct);
        var resolver = new LineResolver(db);
        await resolver.LoadAllAsync(q.Lines.Where(l => l.SkuId is not null).Select(l => l.SkuId!.Value), ct);
        var order = q.ConvertToOrder(r.LocationId, r.Mode, resolver.Skus, numbers.Today, user.UserId, clock.GetUtcNow());
        db.SalesOrders.Add(order);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await sales.GetAsync(order.Id, ct);
    }

    public async Task<QuotationDto> CancelAsync(Guid id, CancellationToken ct)
    {
        var q = await LoadAsync(id, ct);
        q.Cancel();
        await db.SaveChangesAsync(ct);
        return q.ToDto(await CustomerNameAsync(q.CustomerId, ct));
    }

    private async Task<string?> CustomerNameAsync(Guid? id, CancellationToken ct)
        => id is { } cid ? await db.Customers.AsNoTracking().Where(c => c.Id == cid).Select(c => c.Name).FirstOrDefaultAsync(ct) : null;

    private async Task<Quotation> LoadAsync(Guid id, CancellationToken ct, bool tracking = true)
    {
        var q = db.Quotations.Include(x => x.Lines).AsQueryable();
        if (!tracking) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("ใบเสนอราคา", id);
    }
}
