using Bansang.Application.Abstractions;
using Bansang.Application.Contracts;
using Bansang.Domain.Catalog;
using Bansang.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Bansang.Application.Catalog;

public sealed class CatalogService(IAppDbContext db)
{
    // ---------- Products ----------

    public async Task<PagedResult<ProductDto>> ListProductsAsync(PageQuery q, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, term) || EF.Functions.ILike(p.Category!, term));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.Name).Skip(q.Skip).Take(q.SafePageSize).ToListAsync(ct);
        return new(items.Select(p => p.ToDto()).ToList(), q.SafePage, q.SafePageSize, total);
    }

    public async Task<ProductDto> GetProductAsync(Guid id, CancellationToken ct)
        => (await FindProductAsync(id, ct)).ToDto();

    public async Task<ProductDto> CreateProductAsync(SaveProductRequest r, CancellationToken ct)
    {
        var p = new Product(r.Name, r.Category);
        db.Products.Add(p);
        await db.SaveChangesAsync(ct);
        return p.ToDto();
    }

    public async Task<ProductDto> UpdateProductAsync(Guid id, SaveProductRequest r, CancellationToken ct)
    {
        var p = await FindProductAsync(id, ct);
        p.Rename(r.Name, r.Category);
        await db.SaveChangesAsync(ct);
        return p.ToDto();
    }

    // ---------- SKUs ----------

    public async Task<PagedResult<SkuListItemDto>> ListSkusAsync(SkuSearchQuery q, CancellationToken ct)
    {
        var query = db.Skus.AsNoTracking();
        if (q.ProductId is { } pid) query = query.Where(s => s.ProductId == pid);
        if (q.TrackingPattern is { } tp) query = query.Where(s => s.TrackingPattern == tp);
        if (q.IsActive is { } active) query = query.Where(s => s.IsActive == active);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var raw = q.Search.Trim();
            var term = $"%{raw}%";
            query = query.Where(s => EF.Functions.ILike(s.Code, term) || EF.Functions.ILike(s.Name, term)
                                     || EF.Functions.ILike(s.Spec!, term)
                                     || s.Units.Any(u => u.Barcode == raw));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.Code)
            .Skip(q.Skip).Take(q.SafePageSize)
            .Join(db.Products, s => s.ProductId, p => p.Id, (s, p) => new SkuListItemDto(
                s.Id, s.Code, s.Name, s.Spec, s.BaseUnit, s.TrackingPattern, s.IsActive, p.Name))
            .ToListAsync(ct);
        return new(items, q.SafePage, q.SafePageSize, total);
    }

    public async Task<SkuDto> GetSkuAsync(Guid id, CancellationToken ct) => (await FindSkuAsync(id, ct)).ToDto();

    public async Task<BarcodeLookupDto> FindByBarcodeAsync(string barcode, CancellationToken ct)
    {
        var skuId = await db.SkuUnits.Where(u => u.Barcode == barcode).Select(u => (Guid?)u.SkuId).FirstOrDefaultAsync(ct)
                    ?? throw new NotFoundException("บาร์โค้ด", barcode);
        var sku = await FindSkuAsync(skuId, ct);
        var unit = sku.Units.Single(u => u.Barcode == barcode);
        return new(sku.ToDto(), unit.ToDto());
    }

    public async Task<SkuDto> CreateSkuAsync(CreateSkuRequest r, CancellationToken ct)
    {
        _ = await FindProductAsync(r.ProductId, ct);
        if (await db.Skus.AnyAsync(s => s.Code == r.Code.Trim(), ct))
            throw new ConflictException("sku_code_duplicate", $"รหัส SKU '{r.Code}' มีอยู่แล้ว");

        var sku = new Sku(r.ProductId, r.Code, r.Name, r.Spec, r.BaseUnit, r.TrackingPattern, r.BaseForm, r.BaseBarcode);
        foreach (var u in r.Units ?? [])
            sku.AddUnit(u.UnitName, ResolveFactor(u.FactorToBase, u.UnitsPerBase), u.IsExact, u.Form, u.Barcode, u.CanSell, u.CanBuy);
        foreach (var p in r.Prices ?? [])
            sku.SetPrice(p.UnitName, p.Tier, p.MinQty, p.Price);

        await EnsureBarcodesUniqueAsync(sku, ct);
        db.Skus.Add(sku);
        await db.SaveChangesAsync(ct);
        return sku.ToDto();
    }

    public Task<SkuDto> UpdateSkuAsync(Guid id, UpdateSkuRequest r, CancellationToken ct)
        => MutateSkuAsync(id, s => s.UpdateInfo(r.Name, r.Spec, r.TrackingPattern, r.IsActive), ct);

    public Task<SkuDto> AddUnitAsync(Guid id, SkuUnitInput u, CancellationToken ct)
        => MutateSkuAsync(id, s => s.AddUnit(u.UnitName, ResolveFactor(u.FactorToBase, u.UnitsPerBase), u.IsExact, u.Form,
            u.Barcode, u.CanSell, u.CanBuy), ct);

    public Task<SkuDto> UpdateUnitAsync(Guid id, string unit, UpdateSkuUnitRequest r, CancellationToken ct)
        => MutateSkuAsync(id, s => s.UpdateUnit(unit, r.Form, r.Barcode, r.CanSell, r.CanBuy), ct);

    /// <summary>แก้สูตรแปลง — movement เก่าเก็บ factor ของมันเองไว้แล้ว ประวัติไม่เพี้ยน</summary>
    public Task<SkuDto> ChangeUnitFactorAsync(Guid id, string unit, ChangeFactorRequest r, CancellationToken ct)
        => MutateSkuAsync(id, s => s.ChangeUnitFactor(unit, ResolveFactor(r.FactorToBase, r.UnitsPerBase), r.IsExact), ct);

    public Task<SkuDto> RemoveUnitAsync(Guid id, string unit, CancellationToken ct)
        => MutateSkuAsync(id, s => s.RemoveUnit(unit), ct);

    public Task<SkuDto> SetPriceAsync(Guid id, PriceInput p, CancellationToken ct)
        => MutateSkuAsync(id, s => s.SetPrice(p.UnitName, p.Tier, p.MinQty, p.Price), ct);

    public Task<SkuDto> RemovePriceAsync(Guid id, string unit, CustomerTier tier, decimal minQty, CancellationToken ct)
        => MutateSkuAsync(id, s => s.RemovePrice(unit, tier, minQty), ct);

    public async Task<PriceQuoteDto> QuotePriceAsync(Guid id, string unit, CustomerTier tier, decimal qty, CancellationToken ct)
    {
        Quantity.EnsurePositive(qty);
        var sku = await FindSkuAsync(id, ct, tracking: false);
        var rule = sku.ResolvePrice(unit, tier, qty)
                   ?? throw new NotFoundException("ราคา", $"{sku.Code} / {unit} / {tier}");
        return new(sku.Id, rule.UnitName, tier, rule.Tier, qty, rule.MinQty, rule.Price, Quantity.RoundMoney(rule.Price * qty));
    }

    // ---------- helpers ----------

    internal static decimal ResolveFactor(decimal? factorToBase, decimal? unitsPerBase) => (factorToBase, unitsPerBase) switch
    {
        ({ } f, null) => f,
        (null, { } per) when per > 0 => 1m / per,
        (null, { }) => throw new DomainException("factor_not_positive", "จำนวนต่อหน่วยฐานต้องมากกว่า 0"),
        _ => throw new DomainException("factor_required", "ต้องระบุ factorToBase หรือ unitsPerBase อย่างใดอย่างหนึ่ง"),
    };

    private async Task<SkuDto> MutateSkuAsync(Guid id, Action<Sku> mutate, CancellationToken ct)
    {
        var sku = await FindSkuAsync(id, ct);
        mutate(sku);
        await EnsureBarcodesUniqueAsync(sku, ct);
        await db.SaveChangesAsync(ct);
        return sku.ToDto();
    }

    private async Task EnsureBarcodesUniqueAsync(Sku sku, CancellationToken ct)
    {
        var codes = sku.Units.Where(u => u.Barcode != null).Select(u => u.Barcode!).ToList();
        var dup = codes.GroupBy(c => c).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null) throw new ConflictException("barcode_duplicate", $"บาร์โค้ด {dup.Key} ซ้ำกันใน SKU เดียวกัน");
        var taken = await db.SkuUnits.AsNoTracking()
            .Where(u => u.SkuId != sku.Id && u.Barcode != null && codes.Contains(u.Barcode))
            .Select(u => u.Barcode).FirstOrDefaultAsync(ct);
        if (taken is not null) throw new ConflictException("barcode_duplicate", $"บาร์โค้ด {taken} ถูกใช้กับ SKU อื่นแล้ว");
    }

    private async Task<Product> FindProductAsync(Guid id, CancellationToken ct)
        => await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("สินค้า", id);

    internal static async Task<Sku> LoadSkuAsync(IAppDbContext db, Guid id, CancellationToken ct, bool tracking = true)
    {
        var q = db.Skus.Include(s => s.Units).Include(s => s.PriceRules).AsSplitQuery();
        if (!tracking) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("SKU", id);
    }

    private Task<Sku> FindSkuAsync(Guid id, CancellationToken ct, bool tracking = true) => LoadSkuAsync(db, id, ct, tracking);
}
