using System.Net;
using Bansang.Application.Contracts;
using Bansang.Domain.Catalog;

namespace Bansang.Api.IntegrationTests;

public class CatalogTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Create_sku_with_units_prices_and_lookup_by_barcode()
    {
        var http = factory.Client();
        var s = await Scenario.NailAsync(http);
        await http.PutOk<SkuDto>($"/api/skus/{s.SkuId}/prices", new PriceInput("kg", CustomerTier.Retail, 0, 55));
        await http.PutOk<SkuDto>($"/api/skus/{s.SkuId}/prices", new PriceInput("kg", CustomerTier.Technician, 0, 50));
        var sku = await http.PutOk<SkuDto>($"/api/skus/{s.SkuId}/prices", new PriceInput("ลัง", CustomerTier.Retail, 0, 1150));

        Assert.Equal(4, sku.Units.Count);
        Assert.True(sku.Units[0].IsBase);
        Assert.Equal(170m, sku.Units.Single(u => u.UnitName == "ตัว").UnitsPerBase);

        var barcode = sku.Units.Single(u => u.UnitName == "ลัง").Barcode!;
        var hit = await http.GetOk<BarcodeLookupDto>($"/api/skus/by-barcode/{barcode}");
        Assert.Equal(s.SkuId, hit.Sku.Id);
        Assert.Equal("ลัง", hit.Unit.UnitName);

        var quote = await http.GetOk<PriceQuoteDto>($"/api/skus/{s.SkuId}/price?unit=kg&tier=Technician&qty=3");
        Assert.Equal(50m, quote.UnitPrice);
        Assert.Equal(150m, quote.Total);

        var list = await http.GetOk<PagedResult<SkuListItemDto>>($"/api/skus?search={sku.Code}");
        Assert.Equal(1, list.Total);
        var byBarcode = await http.GetOk<PagedResult<SkuListItemDto>>($"/api/skus?search={barcode}");
        Assert.Equal(sku.Id, Assert.Single(byBarcode.Items).Id);
    }

    [Fact]
    public async Task Duplicate_code_and_barcode_are_conflicts()
    {
        var http = factory.Client();
        var s = await Scenario.NailAsync(http);
        var sku = await http.GetOk<SkuDto>($"/api/skus/{s.SkuId}");

        var (status, code) = await http.PostFail("/api/skus", new CreateSkuRequest(sku.ProductId, sku.Code, "x", null, "kg",
            TrackingPattern.SingleUnit));
        Assert.Equal(409, status);
        Assert.Equal("sku_code_duplicate", code);

        var barcode = sku.Units.Single(u => u.UnitName == "ลัง").Barcode;
        (status, code) = await http.PostFail("/api/skus", new CreateSkuRequest(sku.ProductId, $"{sku.Code}-X", "x", null, "kg",
            TrackingPattern.SingleUnit, BaseBarcode: barcode));
        Assert.Equal(409, status);
        Assert.Equal("barcode_duplicate", code);
    }

    [Fact]
    public async Task Invariant_violations_return_422_and_missing_return_404()
    {
        var http = factory.Client();
        var s = await Scenario.NailAsync(http);
        var res = await http.PutAsync($"/api/skus/{s.SkuId}/units/kg/factor",
            System.Net.Http.Json.JsonContent.Create(new ChangeFactorRequest(2, null, true), options: ApiFactory.Json));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);

        var (status, code) = await http.PostFail($"/api/skus/{s.SkuId}/units", new SkuUnitInput("ลัง", 10, null, true, StockForm.Sealed, null));
        Assert.Equal(422, status);
        Assert.Equal("unit_duplicate", code);

        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/api/skus/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Made_to_order_sku_is_not_stocked()
    {
        var http = factory.Client();
        var s = await Scenario.NailAsync(http);
        var p = await http.PostOk<ProductDto>("/api/products", new SaveProductRequest("เมทัลชีท", "หลังคา"));
        var sheet = await http.PostOk<SkuDto>("/api/skus", new CreateSkuRequest(p.Id, $"MS-{Guid.NewGuid():N}"[..12], "เมทัลชีท สั่งตัด",
            null, "เมตร", TrackingPattern.MadeToOrder));
        var (status, code) = await http.PostFail("/api/stock/receive", new ReceiveRequest(sheet.Id, s.Front, "เมตร", 10, 100, null, null));
        Assert.Equal(422, status);
        Assert.Equal("sku_not_stocked", code);
    }
}
