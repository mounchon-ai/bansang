using Bansang.Application.Contracts;
using Bansang.Domain.Catalog;
using Bansang.Domain.Inventory;
using Bansang.Domain.Sales;

namespace Bansang.Api.IntegrationTests;

public class SalesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static PaymentInput Cash(decimal amount) => new(PaymentMethod.Cash, amount);

    private static async Task<SalesOrderDto> Confirm(HttpClient http, Guid id, params PaymentInput[] payments)
        => await http.PostOk<SalesOrderDto>($"/api/sales/{id}/confirm", new ConfirmSalesOrderRequest(payments));

    [Fact]
    public async Task Cash_sale_many_units_one_bill_opens_box_and_prints_vat_receipt()
    {
        var s = await Scenario.NailAsync(factory.Client("cashier"));
        await s.PriceNailAsync();
        await s.Receive("ลัง", 5);
        await s.Http.PostOk<StockOperationResult>("/api/stock/break-bulk", new BreakBulkRequest(s.SkuId, s.Front, "ลัง", 1, null));
        await s.Issue("kg", 23.8m);                                   // Loose 1.2 kg, Sealed 4 ลัง
        var sku = await s.Http.GetOk<SkuDto>($"/api/skus/{s.SkuId}");
        var boxBarcode = sku.Units.Single(u => u.UnitName == "ลัง").Barcode;

        var draft = await s.Http.PostOk<SalesOrderDto>("/api/sales", new CreateSalesOrderRequest(s.Front, Lines:
        [
            new SalesLineInput(3, s.SkuId, "kg"),
            new SalesLineInput(1, Barcode: boxBarcode),
            new SalesLineInput(50, s.SkuId, "ตัว"),
            new SalesLineInput(1, Description: "ค่าตัดเหล็ก", Unit: "ครั้ง", UnitPrice: 20),
        ]));
        Assert.Equal(SalesOrderStatus.Draft, draft.Status);
        Assert.Null(draft.Number);
        Assert.Equal(1360m, draft.Total);                             // 165 + 1150 + 25 + 20
        Assert.Equal(AvailabilityStatus.NeedsBreakBulk, draft.Lines[0].Availability!.Status);
        Assert.Equal(1, draft.Lines[0].Availability!.BoxesToOpen);
        Assert.Equal(AvailabilityStatus.Sufficient, draft.Lines[1].Availability!.Status);

        var done = await Confirm(s.Http, draft.Id, Cash(1500));
        Assert.Equal(SalesOrderStatus.Closed, done.Status);
        Assert.StartsWith("INV", done.Number);
        Assert.Equal(140m, done.ChangeAmount);
        Assert.Single(done.Fulfillments);

        // Sealed: 100 − 25 (แกะ) − 25 (ขายยกลัง) = 50; Loose: 1.2 + 25 − 3 − 0.294 = 22.906
        Assert.Equal((50m, 22.906m), await s.Balance());
        var ledger = await s.Http.GetOk<PagedResult<MovementDto>>($"/api/stock/movements?skuId={s.SkuId}&refDocument={done.Number}");
        Assert.Contains(ledger.Items, m => m.Type == MovementType.BreakBulkOut);
        Assert.Equal(3, ledger.Items.Count(m => m.Type == MovementType.Issue));

        var receipt = await s.Http.GetOk<ReceiptDto>($"/api/sales/{done.Id}/receipt");
        Assert.Equal("ใบเสร็จรับเงิน / ใบกำกับภาษีอย่างย่อ", receipt.Title);
        Assert.Equal(88.97m, receipt.VatAmount);                      // 1360 × 7/107
        Assert.Equal(1271.03m, receipt.TotalBeforeVat);
        Assert.Equal("cashier", receipt.Cashier);
        Assert.Empty(receipt.PendingPickup);
        await s.AssertLedgerConsistent();
    }

    [Fact]
    public async Task Contractor_deposit_is_reserved_and_picked_up_in_rounds()
    {
        var s = await Scenario.NailAsync(factory.Client());
        var sand = await s.SimpleSkuAsync("ทรายถุง", "ถุง", 45);
        await s.Http.PostOk<StockOperationResult>("/api/stock/receive", new ReceiveRequest(sand, s.Front, "ถุง", 250, 30, null, null));
        var contractor = await s.Http.PostOk<CustomerDto>("/api/customers", new SaveCustomerRequest("ช่างสมชาย", CustomerTier.Contractor, Phone: "0812345678"));
        Assert.StartsWith("C", contractor.Code);

        var order = await s.Http.PostOk<SalesOrderDto>("/api/sales", new CreateSalesOrderRequest(s.Front, FulfillmentMode.Deposit,
            contractor.Id, Lines: [new SalesLineInput(200, sand, "ถุง")]));
        order = await Confirm(s.Http, order.Id, new PaymentInput(PaymentMethod.Transfer, 9000, "SLIP-1"));
        Assert.Equal(SalesOrderStatus.Confirmed, order.Status);

        // ของที่ขายแล้วแต่ยังไม่มารับ ห้ามขายซ้ำ — เหลือขายได้แค่ 50
        var other = await s.Http.PostOk<SalesOrderDto>("/api/sales", new CreateSalesOrderRequest(s.Front, Lines: [new SalesLineInput(60, sand, "ถุง")]));
        Assert.Equal(AvailabilityStatus.OutOfStock, other.Lines[0].Availability!.Status);
        var (status, code) = await s.Http.PostFail($"/api/sales/{other.Id}/confirm", new ConfirmSalesOrderRequest([Cash(2700)]));
        Assert.Equal(409, status);
        Assert.Equal("insufficient_stock", code);

        var deposits = await s.Http.GetOk<List<DepositDto>>($"/api/sales/deposits?customerId={contractor.Id}");
        Assert.Equal(200m, Assert.Single(Assert.Single(deposits).Lines).RemainingQty);

        var lineId = order.Lines[0].Id;
        foreach (var qty in new[] { 80m, 80m })
            order = await s.Http.PostOk<SalesOrderDto>($"/api/sales/{order.Id}/fulfillments", new FulfilRequest([new LineQty(lineId, qty)]));
        Assert.Equal(SalesOrderStatus.PartiallyFulfilled, order.Status);
        (status, code) = await s.Http.PostFail($"/api/sales/{order.Id}/fulfillments", new FulfilRequest([new LineQty(lineId, 41)]));
        Assert.Equal(422, status);
        Assert.Equal("fulfil_exceeds", code);
        order = await s.Http.PostOk<SalesOrderDto>($"/api/sales/{order.Id}/fulfillments", new FulfilRequest([new LineQty(lineId, 40)]));

        Assert.Equal(SalesOrderStatus.Closed, order.Status);
        Assert.Equal(3, order.Fulfillments.Count);
        Assert.Empty(await s.Http.GetOk<List<DepositDto>>($"/api/sales/deposits?customerId={contractor.Id}"));
        var stock = await s.Http.GetOk<PagedResult<BucketDto>>($"/api/stock?skuId={sand}&locationId={s.Front}");
        var bucket = Assert.Single(stock.Items);
        Assert.Equal((50m, 0m), (bucket.OnHandBase, bucket.ReservedBase));
        await s.AssertLedgerConsistent();
    }

    [Fact]
    public async Task Approximate_pieces_picked_up_333_333_334_consume_exactly_the_reservation()
    {
        var s = await Scenario.NailAsync(factory.Client());
        await s.PriceNailAsync();
        await s.Receive("kg", 10, unitCost: 36);
        var order = await s.Http.PostOk<SalesOrderDto>("/api/sales", new CreateSalesOrderRequest(s.Front, FulfillmentMode.Deposit,
            Lines: [new SalesLineInput(1000, s.SkuId, "ตัว")]));
        order = await Confirm(s.Http, order.Id, Cash(500));
        Assert.Equal(5.882m, order.Lines[0].QtyBase);

        foreach (var q in new[] { 333m, 333m, 334m })
            order = await s.Http.PostOk<SalesOrderDto>($"/api/sales/{order.Id}/fulfillments", new FulfilRequest([new LineQty(order.Lines[0].Id, q)]));

        Assert.Equal(SalesOrderStatus.Closed, order.Status);
        var loose = (await s.Http.GetOk<PagedResult<BucketDto>>($"/api/stock?skuId={s.SkuId}&locationId={s.Front}&form=Loose")).Items.Single();
        Assert.Equal(4.118m, loose.OnHandBase);
        Assert.Equal(0m, loose.ReservedBase);
    }

    [Fact]
    public async Task Cancel_confirmed_deposit_releases_reservation_and_refunds()
    {
        var s = await Scenario.NailAsync(factory.Client());
        await s.PriceNailAsync();
        await s.Receive("ลัง", 1);
        var order = await s.Http.PostOk<SalesOrderDto>("/api/sales", new CreateSalesOrderRequest(s.Front, FulfillmentMode.Deposit,
            Lines: [new SalesLineInput(3, s.SkuId, "kg"), new SalesLineInput(10, s.SkuId, "ตัว")]));
        // ยังไม่มีกอง Loose: บรรทัดแรกแกะลังและสร้างกองใน transaction — บรรทัดที่สองต้องใช้กองเดียวกัน ไม่สร้างซ้ำ
        order = await Confirm(s.Http, order.Id, Cash(200));
        Assert.Equal(21.941m, (await s.Http.GetOk<PagedResult<BucketDto>>($"/api/stock?skuId={s.SkuId}&form=Loose")).Items.Single().AvailableBase);

        order = await s.Http.PostOk<SalesOrderDto>($"/api/sales/{order.Id}/cancel", new CancelSalesOrderRequest("ลูกค้าเปลี่ยนใจ"));
        Assert.Equal(SalesOrderStatus.Cancelled, order.Status);
        Assert.Equal(170m, order.RefundedAmount);
        var loose = (await s.Http.GetOk<PagedResult<BucketDto>>($"/api/stock?skuId={s.SkuId}&form=Loose")).Items.Single();
        Assert.Equal((25m, 0m), (loose.OnHandBase, loose.ReservedBase));
    }

    [Fact]
    public async Task Return_puts_goods_back_and_refunds_only_what_was_received()
    {
        var s = await Scenario.NailAsync(factory.Client());
        await s.PriceNailAsync();
        await s.Receive("ลัง", 2);
        var order = await s.Http.PostOk<SalesOrderDto>("/api/sales", new CreateSalesOrderRequest(s.Front,
            Lines: [new SalesLineInput(2, s.SkuId, "ลัง")]));
        order = await Confirm(s.Http, order.Id, Cash(2300));
        Assert.Equal((0m, 0m), await s.Balance());

        var (status, code) = await s.Http.PostFail($"/api/sales/{order.Id}/returns",
            new ReturnRequest([new LineQty(order.Lines[0].Id, 3)], "ของเสีย"));
        Assert.Equal((422, "return_exceeds"), (status, code));

        order = await s.Http.PostOk<SalesOrderDto>($"/api/sales/{order.Id}/returns",
            new ReturnRequest([new LineQty(order.Lines[0].Id, 1)], "ลังบุบ", PaymentMethod.Transfer));
        var ret = Assert.Single(order.Returns);
        Assert.StartsWith("RT", ret.Number);
        Assert.Equal(1150m, ret.RefundAmount);
        Assert.Equal(1150m, order.RefundedAmount);
        Assert.Equal((25m, 0m), await s.Balance());
        var ledger = await s.Http.GetOk<PagedResult<MovementDto>>($"/api/stock/movements?skuId={s.SkuId}&type=Return");
        Assert.Equal("ลังบุบ", Assert.Single(ledger.Items).Reason);
        await s.AssertLedgerConsistent();
    }

    [Fact]
    public async Task Customer_tier_pricing_manual_price_and_draft_editing()
    {
        var s = await Scenario.NailAsync(factory.Client());
        await s.PriceNailAsync();
        var tech = await s.Http.PostOk<CustomerDto>("/api/customers", new SaveCustomerRequest("ช่างเอ", CustomerTier.Technician));

        var order = await s.Http.PostOk<SalesOrderDto>("/api/sales", new CreateSalesOrderRequest(s.Front, Lines: [new SalesLineInput(10, s.SkuId, "kg")]));
        Assert.Equal(55m, order.Lines[0].UnitPrice);
        order = await s.Http.PutOk<SalesOrderDto>($"/api/sales/{order.Id}", new UpdateSalesOrderRequest(tech.Id, FulfillmentMode.PickupNow));
        Assert.Equal(50m, order.Lines[0].UnitPrice);
        Assert.Equal("ช่างเอ", order.CustomerName);

        var (status, code) = await s.Http.PostFail($"/api/sales/{order.Id}/lines", new SalesLineInput(1, s.SkuId, "ถุง"));
        Assert.Equal((422, "price_missing"), (status, code));
        order = await s.Http.PostOk<SalesOrderDto>($"/api/sales/{order.Id}/lines", new SalesLineInput(2, s.SkuId, "ถุง", UnitPrice: 28));
        Assert.Equal(PriceSource.Manual, order.Lines[1].PriceSource);

        order = await s.Http.PutOk<SalesOrderDto>($"/api/sales/{order.Id}/lines/{order.Lines[0].Id}", new UpdateSalesLineRequest(4, Discount: 10));
        Assert.Equal(190m, order.Lines[0].LineTotal);
        var del = await s.Http.DeleteAsync($"/api/sales/{order.Id}/lines/{order.Lines[1].Id}");
        Assert.True(del.IsSuccessStatusCode);
        order = await s.Http.GetOk<SalesOrderDto>($"/api/sales/{order.Id}");
        Assert.Single(order.Lines);

        (status, code) = await s.Http.PostFail($"/api/sales/{order.Id}/confirm", new ConfirmSalesOrderRequest([Cash(100)]));
        Assert.Equal((422, "payment_insufficient"), (status, code));
    }

    [Fact]
    public async Task Quotation_converts_to_order_with_locked_prices()
    {
        var s = await Scenario.NailAsync(factory.Client());
        await s.PriceNailAsync();
        await s.Receive("ลัง", 4);
        var q = await s.Http.PostOk<QuotationDto>("/api/quotations", new CreateQuotationRequest(
            [new SalesLineInput(2, s.SkuId, "ลัง", UnitPrice: 1100), new SalesLineInput(1, Description: "ค่าส่ง", Unit: "เที่ยว", UnitPrice: 300)]));
        Assert.StartsWith("QT", q.Number);
        Assert.Equal(2500m, q.Total);

        await s.Http.PutOk<SkuDto>($"/api/skus/{s.SkuId}/prices", new PriceInput("ลัง", CustomerTier.Retail, 0, 1250)); // ราคาขึ้น
        var order = await s.Http.PostOk<SalesOrderDto>($"/api/quotations/{q.Id}/convert", new ConvertQuotationRequest(s.Front, FulfillmentMode.Delivery));
        Assert.Equal(1100m, order.Lines[0].UnitPrice);
        Assert.Equal(PriceSource.Quotation, order.Lines[0].PriceSource);
        Assert.Equal(q.Id, order.QuotationId);
        Assert.Equal(QuotationStatus.Converted, (await s.Http.GetOk<QuotationDto>($"/api/quotations/{q.Id}")).Status);

        var (status, code) = await s.Http.PostFail($"/api/quotations/{q.Id}/convert", new ConvertQuotationRequest(s.Front));
        Assert.Equal((422, "quotation_not_open"), (status, code));
    }

    [Fact]
    public async Task Two_bills_racing_for_the_last_stock_only_one_wins_and_numbers_do_not_collide()
    {
        var s = await Scenario.NailAsync(factory.Client());
        await s.PriceNailAsync();
        await s.Receive("kg", 10, unitCost: 36);

        var drafts = new List<Guid>();
        for (var i = 0; i < 2; i++)
            drafts.Add((await s.Http.PostOk<SalesOrderDto>("/api/sales", new CreateSalesOrderRequest(s.Front, FulfillmentMode.Deposit,
                Lines: [new SalesLineInput(8, s.SkuId, "kg")]))).Id);
        var codes = await Task.WhenAll(drafts.Select(async id =>
        {
            using var c = factory.Client();
            var res = await c.PostAsync($"/api/sales/{id}/confirm", System.Net.Http.Json.JsonContent.Create(
                new ConfirmSalesOrderRequest([Cash(440)]), options: ApiFactory.Json));
            return (int)res.StatusCode;
        }));
        Assert.Equal([200, 409], codes.Order());

        // ยืนยันพร้อมกันหลายบิล (ของพอ) → เลขบิลไม่ซ้ำ
        await s.Receive("kg", 100, unitCost: 36);
        var many = new List<Guid>();
        for (var i = 0; i < 8; i++)
            many.Add((await s.Http.PostOk<SalesOrderDto>("/api/sales", new CreateSalesOrderRequest(s.Front,
                Lines: [new SalesLineInput(1, s.SkuId, "kg")]))).Id);
        var confirmed = await Task.WhenAll(many.Select(async id => await Confirm(factory.Client(), id, Cash(55))));
        var numbers = confirmed.Select(o => o.Number!).ToList();
        Assert.Equal(8, numbers.Distinct().Count());
        await s.AssertLedgerConsistent();
    }
}
