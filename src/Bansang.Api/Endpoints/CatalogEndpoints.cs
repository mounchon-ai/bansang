using Bansang.Application.Catalog;
using Bansang.Application.Contracts;
using Bansang.Domain.Catalog;

namespace Bansang.Api.Endpoints;

public static class CatalogEndpoints
{
    public static void MapCatalog(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/api/products").WithTags("Products");
        products.MapGet("/", (CatalogService s, [AsParameters] PageQuery q, CancellationToken ct) => s.ListProductsAsync(q, ct));
        products.MapGet("/{id:guid}", (CatalogService s, Guid id, CancellationToken ct) => s.GetProductAsync(id, ct));
        products.MapPost("/", async (CatalogService s, SaveProductRequest r, CancellationToken ct) =>
        {
            var p = await s.CreateProductAsync(r, ct);
            return TypedResults.Created($"/api/products/{p.Id}", p);
        });
        products.MapPut("/{id:guid}", (CatalogService s, Guid id, SaveProductRequest r, CancellationToken ct) => s.UpdateProductAsync(id, r, ct));

        var skus = app.MapGroup("/api/skus").WithTags("SKUs");
        skus.MapGet("/", (CatalogService s, [AsParameters] SkuSearchQuery q, CancellationToken ct) => s.ListSkusAsync(q, ct))
            .WithSummary("ค้นหา SKU (รหัส / ชื่อ / สเปก / บาร์โค้ด) แบบแบ่งหน้า");
        skus.MapGet("/{id:guid}", (CatalogService s, Guid id, CancellationToken ct) => s.GetSkuAsync(id, ct));
        skus.MapGet("/by-barcode/{barcode}", (CatalogService s, string barcode, CancellationToken ct) => s.FindByBarcodeAsync(barcode, ct))
            .WithSummary("สแกนบาร์โค้ด → SKU + หน่วยที่ตรงกับบาร์โค้ดนั้น");
        skus.MapPost("/", async (CatalogService s, CreateSkuRequest r, CancellationToken ct) =>
        {
            var sku = await s.CreateSkuAsync(r, ct);
            return TypedResults.Created($"/api/skus/{sku.Id}", sku);
        });
        skus.MapPut("/{id:guid}", (CatalogService s, Guid id, UpdateSkuRequest r, CancellationToken ct) => s.UpdateSkuAsync(id, r, ct));

        skus.MapPost("/{id:guid}/units", (CatalogService s, Guid id, SkuUnitInput r, CancellationToken ct) => s.AddUnitAsync(id, r, ct));
        skus.MapPut("/{id:guid}/units/{unit}", (CatalogService s, Guid id, string unit, UpdateSkuUnitRequest r, CancellationToken ct)
            => s.UpdateUnitAsync(id, unit, r, ct));
        skus.MapPut("/{id:guid}/units/{unit}/factor", (CatalogService s, Guid id, string unit, ChangeFactorRequest r, CancellationToken ct)
            => s.ChangeUnitFactorAsync(id, unit, r, ct))
            .WithSummary("แก้สูตรแปลงหน่วย (ประวัติเดิมไม่เปลี่ยน เพราะ movement เก็บ factor ไว้แล้ว)");
        skus.MapDelete("/{id:guid}/units/{unit}", (CatalogService s, Guid id, string unit, CancellationToken ct) => s.RemoveUnitAsync(id, unit, ct));

        skus.MapPut("/{id:guid}/prices", (CatalogService s, Guid id, PriceInput r, CancellationToken ct) => s.SetPriceAsync(id, r, ct))
            .WithSummary("ตั้งราคา (หน่วย × ระดับลูกค้า × จำนวนขั้นต่ำ)");
        skus.MapDelete("/{id:guid}/prices", (CatalogService s, Guid id, string unit, CustomerTier tier, decimal minQty, CancellationToken ct)
            => s.RemovePriceAsync(id, unit, tier, minQty, ct));
        skus.MapGet("/{id:guid}/price", (CatalogService s, Guid id, string unit, CustomerTier? tier, decimal? qty, CancellationToken ct)
            => s.QuotePriceAsync(id, unit, tier ?? CustomerTier.Retail, qty ?? 1, ct))
            .WithSummary("คิดราคา: หน่วย × ระดับลูกค้า × จำนวน");
    }
}
