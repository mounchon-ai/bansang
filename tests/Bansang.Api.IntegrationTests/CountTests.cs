using Bansang.Application.Contracts;
using Bansang.Domain.Catalog;
using Bansang.Domain.Inventory;

namespace Bansang.Api.IntegrationTests;

public class CountTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Small_variance_auto_adjusts_large_variance_waits_for_owner()
    {
        var s = await Scenario.NailAsync(factory.Client("warehouse"));
        await s.Receive("ลัง", 4);
        await s.Http.PostOk<StockOperationResult>("/api/stock/break-bulk", new BreakBulkRequest(s.SkuId, s.Front, "ลัง", 1, null));
        // Sealed 75 kg, Loose 25 kg

        var count = await s.Http.PostOk<CountDto>("/api/counts", new CreateCountRequest(s.Front, "นับเย็น", null));
        Assert.Equal(3m, count.ThresholdPercent);
        var loose = await s.Http.PostOk<CountLineDto>($"/api/counts/{count.Id}/lines", new RecordCountLineRequest(s.SkuId, "kg", 24.5m, null));
        var sealedLine = await s.Http.PostOk<CountLineDto>($"/api/counts/{count.Id}/lines", new RecordCountLineRequest(s.SkuId, "ลัง", 2, null));
        Assert.Equal(-2m, loose.VariancePercent);
        Assert.Equal(StockForm.Sealed, sealedLine.Form);
        Assert.Equal(-25m, sealedLine.VarianceBase);

        var submitted = await s.Http.PostOk<CountDto>($"/api/counts/{count.Id}/submit", new { });
        var autoLine = submitted.Lines.Single(l => l.Form == StockForm.Loose);
        var pending = submitted.Lines.Single(l => l.Form == StockForm.Sealed);
        Assert.Equal(CountLineStatus.AutoAdjusted, autoLine.Status);
        Assert.Equal(StockCountLine.AutoAdjustReason, autoLine.Reason);
        Assert.NotNull(autoLine.AdjustmentMovementId);
        Assert.Equal(CountLineStatus.PendingApproval, pending.Status);
        Assert.Equal((75m, 24.5m), await s.Balance());

        var owner = factory.Client("owner");
        var approved = await owner.PostOk<CountLineDto>($"/api/counts/{count.Id}/lines/{pending.Id}/approve",
            new ResolveCountLineRequest("ของหาย"));
        Assert.Equal(CountLineStatus.Approved, approved.Status);
        Assert.Equal("owner", approved.ResolvedBy);
        Assert.Equal((50m, 24.5m), await s.Balance());

        var ledger = await s.Http.GetOk<PagedResult<MovementDto>>($"/api/stock/movements?skuId={s.SkuId}&type=Adjust");
        Assert.Contains(ledger.Items, m => m.Reason == "ของหาย" && m.QtyBase == -25m && m.UserId == "owner");
        await s.AssertLedgerConsistent();
    }

    [Fact]
    public async Task Submitted_count_cannot_be_changed_or_resubmitted()
    {
        var s = await Scenario.NailAsync(factory.Client());
        await s.Receive("kg", 10, unitCost: 36);
        var count = await s.Http.PostOk<CountDto>("/api/counts", new CreateCountRequest(s.Front, null, null));
        await s.Http.PostOk<CountLineDto>($"/api/counts/{count.Id}/lines", new RecordCountLineRequest(s.SkuId, "kg", 10, null));
        var done = await s.Http.PostOk<CountDto>($"/api/counts/{count.Id}/submit", new { });
        Assert.Equal(CountLineStatus.Matched, Assert.Single(done.Lines).Status);

        Assert.Equal(422, (await s.Http.PostFail($"/api/counts/{count.Id}/submit", new { })).Status);
        Assert.Equal(422, (await s.Http.PostFail($"/api/counts/{count.Id}/lines", new RecordCountLineRequest(s.SkuId, "kg", 9, null))).Status);
    }

    [Fact]
    public async Task Repeated_shortages_on_piece_sales_suggest_a_new_factor()
    {
        // สูตร 170 ตัว/kg แต่จริง ๆ ≈165: ขายทีละ 1,700 ตัว (บันทึก 10 kg) แล้วนับขาด 0.3 kg ทุกรอบ
        var s = await Scenario.NailAsync(factory.Client());
        for (var i = 0; i < 3; i++)
        {
            await s.Receive("kg", 30, unitCost: 36);
            await s.Issue("ตัว", 1700);
            var (_, loose) = await s.Balance();
            var count = await s.Http.PostOk<CountDto>("/api/counts", new CreateCountRequest(s.Front, null, null));
            await s.Http.PostOk<CountLineDto>($"/api/counts/{count.Id}/lines", new RecordCountLineRequest(s.SkuId, "kg", loose - 0.3m, null));
            var done = await s.Http.PostOk<CountDto>($"/api/counts/{count.Id}/submit", new { });
            Assert.Equal(CountLineStatus.AutoAdjusted, Assert.Single(done.Lines).Status);
        }

        var stats = await s.Http.GetOk<VarianceStatsDto>($"/api/skus/{s.SkuId}/variance-stats");
        Assert.Equal("ตัว", stats.ApproxUnit);
        Assert.Equal(3, stats.History.Count);
        Assert.NotNull(stats.Suggestion);
        Assert.Equal(165m, Math.Round(stats.Suggestion.SuggestedUnitsPerBase));
    }
}

public class NegativeStockTests(NegativeStockApiFactory factory) : IClassFixture<NegativeStockApiFactory>
{
    [Fact]
    public async Task Negative_sale_is_allowed_flagged_and_suggested_for_count()
    {
        var s = await Scenario.NailAsync(factory.Client());
        await s.Receive("ลัง", 1);
        var r = await s.Issue("kg", 30);                              // แกะลังเดียวที่มี แล้วยังขาด 5 kg

        Assert.Equal(AvailabilityStatus.AllowedNegative, r.Plan!.Status);
        Assert.Equal((0m, -5m), await s.Balance());
        var loose = r.Buckets.Single(b => b.Form == StockForm.Loose);
        Assert.True(loose.NeedsCount);

        var suggestions = await s.Http.GetOk<List<CountSuggestionDto>>($"/api/counts/suggestions?locationId={s.Front}");
        Assert.True(suggestions[0].NeedsCount);
        Assert.Equal(s.SkuId, suggestions[0].SkuId);

        // นับแล้วปรับยอด → ธงหายไป
        var count = await s.Http.PostOk<CountDto>("/api/counts", new CreateCountRequest(s.Front, null, null));
        var line = await s.Http.PostOk<CountLineDto>($"/api/counts/{count.Id}/lines", new RecordCountLineRequest(s.SkuId, "kg", 0, StockForm.Loose));
        await s.Http.PostOk<CountDto>($"/api/counts/{count.Id}/submit", new { });
        await s.Http.PostOk<CountLineDto>($"/api/counts/{count.Id}/lines/{line.Id}/approve", new ResolveCountLineRequest("ขายก่อนรับของเข้า"));
        var page = await s.Http.GetOk<PagedResult<BucketDto>>($"/api/stock?skuId={s.SkuId}&locationId={s.Front}&form=Loose");
        Assert.False(Assert.Single(page.Items).NeedsCount);
        Assert.Equal(0m, page.Items[0].OnHandBase);
    }
}
