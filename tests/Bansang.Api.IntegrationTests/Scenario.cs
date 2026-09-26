using Bansang.Application.Contracts;
using Bansang.Domain.Catalog;

namespace Bansang.Api.IntegrationTests;

/// <summary>สร้างข้อมูลตั้งต้นต่อ test — SKU รหัสไม่ซ้ำ เพื่อให้ test แยกกันได้บน DB เดียว</summary>
public sealed class Scenario(HttpClient http)
{
    public HttpClient Http { get; } = http;
    public Guid SkuId { get; private set; }
    public Guid Front { get; private set; }
    public Guid Warehouse { get; private set; }

    public static async Task<Scenario> NailAsync(HttpClient http)
    {
        var s = new Scenario(http);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var product = await http.PostOk<ProductDto>("/api/products", new SaveProductRequest("ตะปู", "ฮาร์ดแวร์"));
        var sku = await http.PostOk<SkuDto>("/api/skus", new CreateSkuRequest(
            product.Id, $"NAIL-{tag}", "ตะปู 2 นิ้ว", "2 นิ้ว", "kg", TrackingPattern.PackWeighApprox,
            Units:
            [
                new SkuUnitInput("ลัง", 25, null, true, StockForm.Sealed, $"B{tag}"),
                new SkuUnitInput("ถุง", 0.5m, null, true, StockForm.Loose, null),
                new SkuUnitInput("ตัว", null, 170, false, StockForm.Loose, null),
            ]));
        s.SkuId = sku.Id;
        s.Front = (await http.PostOk<LocationDto>("/api/locations", new CreateLocationRequest($"F-{tag}", "หน้าร้าน"))).Id;
        s.Warehouse = (await http.PostOk<LocationDto>("/api/locations", new CreateLocationRequest($"W-{tag}", "โกดัง"))).Id;
        return s;
    }

    public Task<StockOperationResult> Receive(string unit, decimal qty, decimal unitCost = 900, Guid? location = null)
        => Http.PostOk<StockOperationResult>("/api/stock/receive",
            new ReceiveRequest(SkuId, location ?? Front, unit, qty, unitCost, "PO-TEST", null));

    public Task<StockOperationResult> Issue(string unit, decimal qty, bool autoBreakBulk = true, bool fromReserved = false)
        => Http.PostOk<StockOperationResult>("/api/stock/issue",
            new IssueRequest(SkuId, Front, unit, qty, "INV-TEST", autoBreakBulk, fromReserved));

    public async Task<(decimal Sealed, decimal Loose)> Balance(Guid? location = null)
    {
        var page = await Http.GetOk<PagedResult<BucketDto>>($"/api/stock?skuId={SkuId}&locationId={location ?? Front}");
        decimal Of(StockForm f) => page.Items.FirstOrDefault(b => b.Form == f)?.OnHandBase ?? 0;
        return (Of(StockForm.Sealed), Of(StockForm.Loose));
    }

    public async Task AssertLedgerConsistent()
    {
        var mismatches = await Http.GetOk<List<ReconcileMismatchDto>>("/api/stock/reconcile");
        Assert.Empty(mismatches);
    }
}
