using Bansang.Application.Contracts;
using Bansang.Domain.Catalog;
using Bansang.Domain.Inventory;

namespace Bansang.Api.IntegrationTests;

public class StockFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Receive_converts_to_base_and_sets_average_cost()
    {
        var s = await Scenario.NailAsync(factory.Client());
        var r = await s.Receive("ลัง", 9, unitCost: 900);           // เอกสาร section 8

        var m = Assert.Single(r.Movements);
        Assert.Equal(MovementType.Receive, m.Type);
        Assert.Equal(225m, m.QtyBase);
        Assert.Equal(36m, m.CostPerBase);
        Assert.Equal(StockForm.Sealed, m.Form);
        Assert.Equal((225m, 0m), await s.Balance());
        var sku = await s.Http.GetOk<SkuDto>($"/api/skus/{s.SkuId}");
        Assert.Equal(36m, sku.AvgCostPerBase);

        await s.Receive("ลัง", 1, unitCost: 1000);                   // (225×36 + 25×40)/250 = 36.4
        sku = await s.Http.GetOk<SkuDto>($"/api/skus/{s.SkuId}");
        Assert.Equal(36.4m, sku.AvgCostPerBase);
    }

    [Fact]
    public async Task Doc_sequence_selling_3kg_opens_one_box_in_same_transaction()
    {
        // เตรียมสภาพตามเอกสาร section 7: Loose 1.2 kg, Sealed 4 ลัง
        var s = await Scenario.NailAsync(factory.Client());
        await s.Receive("ลัง", 5);
        await s.Http.PostOk<StockOperationResult>("/api/stock/break-bulk", new BreakBulkRequest(s.SkuId, s.Front, "ลัง", 1, null));
        await s.Issue("kg", 23.8m);
        Assert.Equal((100m, 1.2m), await s.Balance());

        var plan = await s.Http.GetOk<AvailabilityPlan>(
            $"/api/stock/availability?skuId={s.SkuId}&locationId={s.Front}&unit=kg&qty=3");
        Assert.Equal(AvailabilityStatus.NeedsBreakBulk, plan.Status);
        Assert.Equal(1.8m, plan.ShortageBase);
        Assert.Equal(1, plan.BoxesToOpen);

        var r = await s.Issue("kg", 3);

        Assert.Equal([MovementType.BreakBulkOut, MovementType.BreakBulkIn, MovementType.Issue], r.Movements.Select(m => m.Type));
        Assert.Equal([-25m, 25m, -3m], r.Movements.Select(m => m.QtyBase));
        Assert.Equal(r.Movements[0].CorrelationId, r.Movements[1].CorrelationId);
        Assert.Equal((75m, 23.2m), await s.Balance());
        await s.AssertLedgerConsistent();
    }

    [Fact]
    public async Task Without_auto_break_bulk_the_sale_is_rejected_and_nothing_changes()
    {
        var s = await Scenario.NailAsync(factory.Client());
        await s.Receive("ลัง", 2);
        var (status, code) = await s.Http.PostFail("/api/stock/issue",
            new IssueRequest(s.SkuId, s.Front, "kg", 3, null, AutoBreakBulk: false));
        Assert.Equal(409, status);
        Assert.Equal("insufficient_stock", code);
        Assert.Equal((50m, 0m), await s.Balance());
    }

    [Fact]
    public async Task Selling_pieces_uses_approximate_factor_and_keeps_snapshot()
    {
        var s = await Scenario.NailAsync(factory.Client());
        await s.Receive("kg", 10, unitCost: 36);
        var r = await s.Issue("ตัว", 50);

        var m = Assert.Single(r.Movements);
        Assert.Equal(-0.294m, m.QtyBase);
        Assert.True(m.IsApproximate);
        Assert.Equal("ตัว", m.InputUnit);
        Assert.Equal(50m, m.InputQty);

        // แก้สูตรเป็น 165 ตัว/kg — ประวัติเดิมต้องยังเป็น 1/170
        await s.Http.PutOk<SkuDto>($"/api/skus/{s.SkuId}/units/ตัว/factor", new ChangeFactorRequest(null, 165, false));
        var ledger = await s.Http.GetOk<PagedResult<MovementDto>>($"/api/stock/movements?skuId={s.SkuId}&type=Issue");
        Assert.Equal(Math.Round(1m / 170m, 12), Assert.Single(ledger.Items).FactorSnapshot);
    }

    [Fact]
    public async Task Selling_whole_boxes_draws_from_sealed_bucket()
    {
        var s = await Scenario.NailAsync(factory.Client());
        await s.Receive("ลัง", 3);
        await s.Issue("ลัง", 2);
        Assert.Equal((25m, 0m), await s.Balance());
        var (status, _) = await s.Http.PostFail("/api/stock/issue", new IssueRequest(s.SkuId, s.Front, "ลัง", 2, null));
        Assert.Equal(409, status);
    }

    [Fact]
    public async Task Reserved_goods_cannot_be_sold_twice_and_are_picked_up_in_rounds()
    {
        var s = await Scenario.NailAsync(factory.Client());
        await s.Receive("kg", 10, unitCost: 36);
        await s.Http.PostOk<StockOperationResult>("/api/stock/reserve", new ReserveRequest(s.SkuId, s.Front, "kg", 8, "SO-1"));

        var (status, _) = await s.Http.PostFail("/api/stock/issue", new IssueRequest(s.SkuId, s.Front, "kg", 3, "SO-2"));
        Assert.Equal(409, status);

        var r = await s.Issue("kg", 5, fromReserved: true);       // มารับรอบแรก
        var loose = r.Buckets.Single(b => b.Form == StockForm.Loose);
        Assert.Equal(5m, loose.OnHandBase);
        Assert.Equal(3m, loose.ReservedBase);
        Assert.Equal(2m, loose.AvailableBase);

        await s.Http.PostOk<StockOperationResult>("/api/stock/release", new ReleaseRequest(s.SkuId, s.Front, "kg", 3, "SO-1"));
        await s.Issue("kg", 5);
        Assert.Equal((0m, 0m), await s.Balance());
    }

    [Fact]
    public async Task Reversing_break_bulk_reverses_both_sides_once()
    {
        var s = await Scenario.NailAsync(factory.Client());
        await s.Receive("ลัง", 2);
        var bb = await s.Http.PostOk<StockOperationResult>("/api/stock/break-bulk", new BreakBulkRequest(s.SkuId, s.Front, null, 1, null));
        Assert.Equal((25m, 25m), await s.Balance());

        var rev = await s.Http.PostOk<StockOperationResult>($"/api/stock/movements/{bb.Movements[1].Id}/reverse",
            new ReverseRequest("แกะผิดลัง"));
        Assert.Equal(2, rev.Movements.Count);
        Assert.All(rev.Movements, m => Assert.Equal(MovementType.Reversal, m.Type));
        Assert.Equal((50m, 0m), await s.Balance());

        var (status, code) = await s.Http.PostFail($"/api/stock/movements/{bb.Movements[0].Id}/reverse", new ReverseRequest("ซ้ำ"));
        Assert.Equal(409, status);
        Assert.Equal("already_reversed", code);
        await s.AssertLedgerConsistent();
    }

    [Fact]
    public async Task Transfer_moves_stock_between_locations()
    {
        var s = await Scenario.NailAsync(factory.Client());
        await s.Receive("ลัง", 4, location: s.Warehouse);
        await s.Http.PostOk<StockOperationResult>("/api/stock/transfer",
            new TransferRequest(s.SkuId, s.Warehouse, s.Front, "ลัง", 1, "TR-1"));
        Assert.Equal((75m, 0m), await s.Balance(s.Warehouse));
        Assert.Equal((25m, 0m), await s.Balance(s.Front));
    }

    [Fact]
    public async Task Manual_adjust_requires_reason()
    {
        var s = await Scenario.NailAsync(factory.Client());
        var (status, code) = await s.Http.PostFail("/api/stock/adjust", new AdjustRequest(s.SkuId, s.Front, "kg", 1, null, " "));
        Assert.Equal(422, status);
        Assert.Equal("reason_required", code);
        await s.Http.PostOk<StockOperationResult>("/api/stock/adjust", new AdjustRequest(s.SkuId, s.Front, "kg", -0.5m, null, "ชั่งผิด"));
        Assert.Equal((0m, -0.5m), await s.Balance());
    }

    [Fact]
    public async Task Concurrent_sales_never_oversell_the_same_bucket()
    {
        var s = await Scenario.NailAsync(factory.Client());
        await s.Receive("kg", 10, unitCost: 36);

        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            using var c = factory.Client();
            var res = await c.PostAsync("/api/stock/issue", System.Net.Http.Json.JsonContent.Create(
                new IssueRequest(s.SkuId, s.Front, "kg", 1, null, AutoBreakBulk: false), options: ApiFactory.Json));
            return (int)res.StatusCode;
        });
        var codes = await Task.WhenAll(tasks);

        Assert.Equal(10, codes.Count(c => c == 200));
        Assert.Equal(10, codes.Count(c => c == 409));
        Assert.Equal((0m, 0m), await s.Balance());
        await s.AssertLedgerConsistent();
    }
}
