using Bansang.Application.Contracts;
using Bansang.Application.Sales;

namespace Bansang.Api.Endpoints;

public static class SalesEndpoints
{
    public static void MapSales(this IEndpointRouteBuilder app)
    {
        var customers = app.MapGroup("/api/customers").WithTags("Customers");
        customers.MapGet("/", (CustomerService s, [AsParameters] CustomerQuery q, CancellationToken ct) => s.ListAsync(q, ct))
            .WithSummary("ค้นลูกค้า (ชื่อ / รหัส / เบอร์ / เลขผู้เสียภาษี)");
        customers.MapGet("/{id:guid}", (CustomerService s, Guid id, CancellationToken ct) => s.GetAsync(id, ct));
        customers.MapPost("/", async (CustomerService s, SaveCustomerRequest r, CancellationToken ct) =>
        {
            var c = await s.CreateAsync(r, ct);
            return TypedResults.Created($"/api/customers/{c.Id}", c);
        });
        customers.MapPut("/{id:guid}", (CustomerService s, Guid id, SaveCustomerRequest r, CancellationToken ct) => s.UpdateAsync(id, r, ct));

        var sales = app.MapGroup("/api/sales").WithTags("Sales / POS");
        sales.MapGet("/", (SalesService s, [AsParameters] SalesQuery q, CancellationToken ct) => s.ListAsync(q, ct));
        sales.MapGet("/deposits", (SalesService s, Guid? customerId, Guid? locationId, CancellationToken ct)
                => s.DepositsAsync(customerId, locationId, ct))
            .WithSummary("ของที่ลูกค้าซื้อฝากไว้ ยังรับไม่ครบ");
        sales.MapGet("/{id:guid}", (SalesService s, Guid id, CancellationToken ct) => s.GetAsync(id, ct));
        sales.MapGet("/{id:guid}/receipt", (SalesService s, Guid id, CancellationToken ct) => s.ReceiptAsync(id, ct))
            .WithSummary("ข้อมูลใบเสร็จ / ใบกำกับภาษีอย่างย่อ (ราคารวม VAT, ถอด VAT แสดง)");

        sales.MapPost("/", async (SalesService s, CreateSalesOrderRequest r, CancellationToken ct) =>
        {
            var o = await s.CreateAsync(r, ct);
            return TypedResults.Created($"/api/sales/{o.Id}", o);
        }).WithSummary("เปิดบิล (Draft) — แต่ละบรรทัดคืนคำเตือนสต็อก เช่น ต้องแกะกี่ลัง");
        sales.MapPut("/{id:guid}", (SalesService s, Guid id, UpdateSalesOrderRequest r, CancellationToken ct) => s.UpdateAsync(id, r, ct))
            .WithSummary("เปลี่ยนลูกค้า (คิดราคาใหม่ตามระดับ) / วิธีรับของ / ส่วนลดท้ายบิล");
        sales.MapPost("/{id:guid}/lines", (SalesService s, Guid id, SalesLineInput r, CancellationToken ct) => s.AddLineAsync(id, r, ct))
            .WithSummary("เพิ่มรายการ: barcode หรือ skuId+unit หรือสินค้าเบ็ดเตล็ด (description+unitPrice)");
        sales.MapPut("/{id:guid}/lines/{lineId:guid}", (SalesService s, Guid id, Guid lineId, UpdateSalesLineRequest r, CancellationToken ct)
            => s.UpdateLineAsync(id, lineId, r, ct));
        sales.MapDelete("/{id:guid}/lines/{lineId:guid}", (SalesService s, Guid id, Guid lineId, CancellationToken ct)
            => s.RemoveLineAsync(id, lineId, ct));

        sales.MapPost("/{id:guid}/confirm", (SalesService s, Guid id, ConfirmSalesOrderRequest r, CancellationToken ct)
                => s.ConfirmAsync(id, r, ct))
            .WithSummary("ยืนยันบิล + รับเงิน → จองของ; ถ้ารับเลยจะตัดสต็อกทันที");
        sales.MapPost("/{id:guid}/fulfillments", (SalesService s, Guid id, FulfilRequest r, CancellationToken ct)
                => s.FulfilAsync(id, r, ct))
            .WithSummary("ลูกค้ามารับของ / รถส่งของออก — ตัดจากของที่จองไว้");
        sales.MapPost("/{id:guid}/cancel", (SalesService s, Guid id, CancelSalesOrderRequest r, CancellationToken ct)
                => s.CancelAsync(id, r, ct))
            .WithSummary("ยกเลิกบิล (ยังไม่มีการรับของ) — ปลดจอง + คืนเงิน");
        sales.MapPost("/{id:guid}/returns", (SalesService s, Guid id, ReturnRequest r, CancellationToken ct)
                => s.ReturnAsync(id, r, ct))
            .WithSummary("คืนของ — สต็อกกลับเข้ากอง + คืนเงิน");

        var quotations = app.MapGroup("/api/quotations").WithTags("Quotations");
        quotations.MapGet("/", (QuotationService s, [AsParameters] QuotationQuery q, CancellationToken ct) => s.ListAsync(q, ct));
        quotations.MapGet("/{id:guid}", (QuotationService s, Guid id, CancellationToken ct) => s.GetAsync(id, ct));
        quotations.MapPost("/", async (QuotationService s, CreateQuotationRequest r, CancellationToken ct) =>
        {
            var q = await s.CreateAsync(r, ct);
            return TypedResults.Created($"/api/quotations/{q.Id}", q);
        });
        quotations.MapPost("/{id:guid}/convert", (QuotationService s, Guid id, ConvertQuotationRequest r, CancellationToken ct)
                => s.ConvertAsync(id, r, ct))
            .WithSummary("แปลงเป็นบิลขาย (Draft) คงราคาที่เสนอไว้");
        quotations.MapPost("/{id:guid}/cancel", (QuotationService s, Guid id, CancellationToken ct) => s.CancelAsync(id, ct));
    }
}
