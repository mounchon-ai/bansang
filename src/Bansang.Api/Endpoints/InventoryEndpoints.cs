using Bansang.Application.Abstractions;
using Bansang.Application.Contracts;
using Bansang.Application.Inventory;
using Bansang.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Bansang.Api.Endpoints;

public static class InventoryEndpoints
{
    public static void MapInventory(this IEndpointRouteBuilder app)
    {
        var locations = app.MapGroup("/api/locations").WithTags("Locations");
        locations.MapGet("/", async (IAppDbContext db, CancellationToken ct) =>
            (await db.Locations.AsNoTracking().OrderBy(l => l.Code).ToListAsync(ct)).Select(l => l.ToDto()));
        locations.MapPost("/", async (IAppDbContext db, CreateLocationRequest r, CancellationToken ct) =>
        {
            var l = new Location(r.Code, r.Name);
            db.Locations.Add(l);
            await db.SaveChangesAsync(ct);
            return TypedResults.Created($"/api/locations/{l.Id}", l.ToDto());
        });
        locations.MapPut("/{id:guid}", async (IAppDbContext db, Guid id, UpdateLocationRequest r, CancellationToken ct) =>
        {
            var l = await db.Locations.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("ที่เก็บ", id);
            l.Update(r.Name, r.IsActive);
            await db.SaveChangesAsync(ct);
            return l.ToDto();
        });

        var stock = app.MapGroup("/api/stock").WithTags("Stock");
        stock.MapGet("/", (StockService s, [AsParameters] StockQuery q, CancellationToken ct) => s.ListStockAsync(q, ct))
            .WithSummary("ยอดคงเหลือแยกกอง (Sealed/Loose × ที่เก็บ) พร้อมแปลงเป็นทุกหน่วย");
        stock.MapGet("/availability", (StockService s, Guid skuId, Guid locationId, string unit, decimal qty, CancellationToken ct)
            => s.CheckAvailabilityAsync(skuId, locationId, unit, qty, ct))
            .WithSummary("เช็คของพอไหม — ถ้า Loose ไม่พอจะบอกว่าต้องแกะกี่ลัง");
        stock.MapGet("/movements", (StockService s, [AsParameters] MovementQuery q, CancellationToken ct) => s.ListMovementsAsync(q, ct))
            .WithSummary("สมุดบัญชีสต็อก (ledger) ย้อนหลัง");
        stock.MapGet("/reconcile", (StockService s, CancellationToken ct) => s.ReconcileAsync(ct))
            .WithSummary("ตรวจยอดกอง = ผลรวม ledger (ควรได้ list ว่าง)");

        stock.MapPost("/receive", (StockService s, ReceiveRequest r, CancellationToken ct) => s.ReceiveAsync(r, ct))
            .WithSummary("รับของเข้า + อัปเดตต้นทุนเฉลี่ย");
        stock.MapPost("/issue", (StockService s, IssueRequest r, CancellationToken ct) => s.IssueAsync(r, ct))
            .WithSummary("ตัดสต็อก (ของออกจากร้าน) — แกะลังอัตโนมัติได้");
        stock.MapPost("/reserve", (StockService s, ReserveRequest r, CancellationToken ct) => s.ReserveAsync(r, ct))
            .WithSummary("จองของ (ขายแล้ว ลูกค้าฝากไว้)");
        stock.MapPost("/release", (StockService s, ReleaseRequest r, CancellationToken ct) => s.ReleaseAsync(r, ct))
            .WithSummary("ยกเลิกการจอง");
        stock.MapPost("/break-bulk", (StockService s, BreakBulkRequest r, CancellationToken ct) => s.BreakBulkAsync(r, ct))
            .WithSummary("แกะลัง: Sealed → Loose");
        stock.MapPost("/transfer", (StockService s, TransferRequest r, CancellationToken ct) => s.TransferAsync(r, ct))
            .WithSummary("โอนระหว่างที่เก็บ (หน้าร้าน ↔ โกดัง)");
        stock.MapPost("/adjust", (StockService s, AdjustRequest r, CancellationToken ct) => s.AdjustAsync(r, ct))
            .WithSummary("ปรับยอดด้วยมือ (ต้องมีเหตุผล)");
        stock.MapPost("/movements/{id:guid}/reverse", (StockService s, Guid id, ReverseRequest r, CancellationToken ct)
            => s.ReverseAsync(id, r, ct))
            .WithSummary("ออกรายการกลับ (ledger แก้ไม่ได้)");

        var counts = app.MapGroup("/api/counts").WithTags("Stock counts");
        counts.MapGet("/", (CountService s, [AsParameters] CountQuery q, CancellationToken ct) => s.ListAsync(q, ct));
        counts.MapGet("/suggestions", (CountService s, Guid locationId, int? take, CancellationToken ct)
            => s.SuggestAsync(locationId, take ?? 10, ct))
            .WithSummary("รายการที่ควรนับ: ของติดธงก่อน แล้วกองแบ่งขายที่ไม่ได้นับนานสุด");
        counts.MapGet("/{id:guid}", (CountService s, Guid id, CancellationToken ct) => s.GetAsync(id, ct));
        counts.MapPost("/", async (CountService s, CreateCountRequest r, CancellationToken ct) =>
        {
            var c = await s.CreateAsync(r, ct);
            return TypedResults.Created($"/api/counts/{c.Id}", c);
        });
        counts.MapPost("/{id:guid}/lines", (CountService s, Guid id, RecordCountLineRequest r, CancellationToken ct)
            => s.RecordLineAsync(id, r, ct));
        counts.MapPost("/{id:guid}/submit", (CountService s, Guid id, CancellationToken ct) => s.SubmitAsync(id, ct))
            .WithSummary("ส่งรอบนับ: ส่วนต่างไม่เกินเกณฑ์ปรับอัตโนมัติ เกินเกณฑ์รออนุมัติ");
        counts.MapPost("/{id:guid}/lines/{lineId:guid}/approve", (CountService s, Guid id, Guid lineId, ResolveCountLineRequest r,
            CancellationToken ct) => s.ApproveAsync(id, lineId, r, ct));
        counts.MapPost("/{id:guid}/lines/{lineId:guid}/reject", (CountService s, Guid id, Guid lineId, ResolveCountLineRequest r,
            CancellationToken ct) => s.RejectAsync(id, lineId, r, ct));

        app.MapGet("/api/skus/{id:guid}/variance-stats", (CountService s, Guid id, string? unit, CancellationToken ct)
                => s.VarianceStatsAsync(id, unit, ct))
            .WithTags("Stock counts")
            .WithSummary("สถิติส่วนต่างรายสินค้า + แนะนำแก้สูตรประมาณ");
    }
}
