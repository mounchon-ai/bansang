using Bansang.Application.Abstractions;
using Bansang.Application.Catalog;
using Bansang.Application.Contracts;
using Bansang.Domain.Catalog;
using Bansang.Domain.Common;
using Bansang.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Bansang.Application.Sales;

/// <summary>แปลง input จาก POS (บาร์โค้ด / SKU+หน่วย / เบ็ดเตล็ด) เป็น SKU ที่โหลดแล้ว — cache ต่อ request</summary>
internal sealed class LineResolver(IAppDbContext db)
{
    private readonly Dictionary<Guid, Sku> _skus = [];

    public IReadOnlyDictionary<Guid, Sku> Skus => _skus;

    public async Task<(Sku? Sku, string Unit)> ResolveAsync(SalesLineInput input, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(input.Barcode))
        {
            var hit = await db.SkuUnits.AsNoTracking().Where(u => u.Barcode == input.Barcode.Trim())
                          .Select(u => new { u.SkuId, u.UnitName }).FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException("บาร์โค้ด", input.Barcode);
            return (await GetAsync(hit.SkuId, ct), hit.UnitName);
        }
        if (input.SkuId is { } skuId)
        {
            var sku = await GetAsync(skuId, ct);
            return (sku, string.IsNullOrWhiteSpace(input.Unit) ? sku.BaseUnit : input.Unit);
        }
        if (string.IsNullOrWhiteSpace(input.Description))
            throw new DomainException("line_target_required", "ต้องระบุ barcode, skuId หรือ description (สินค้าเบ็ดเตล็ด)");
        if (input.UnitPrice is null) throw new DomainException("price_required", "สินค้าเบ็ดเตล็ดต้องกรอกราคา");
        return (null, string.IsNullOrWhiteSpace(input.Unit) ? "ชิ้น" : input.Unit);
    }

    public async Task<Sku> GetAsync(Guid skuId, CancellationToken ct)
    {
        if (_skus.TryGetValue(skuId, out var cached)) return cached;
        var sku = await CatalogService.LoadSkuAsync(db, skuId, ct, tracking: false);
        _skus[skuId] = sku;
        return sku;
    }

    public async Task LoadAllAsync(IEnumerable<Guid> skuIds, CancellationToken ct)
    {
        foreach (var id in skuIds.Distinct()) await GetAsync(id, ct);
    }

    public SalesLine AddTo(SalesOrder order, Sku? sku, string unit, SalesLineInput input)
        => sku is null
            ? order.AddMiscLine(input.Description!, unit, input.Qty, input.UnitPrice!.Value, input.Discount)
            : order.AddLine(sku, unit, input.Qty, input.UnitPrice, input.Discount);
}
