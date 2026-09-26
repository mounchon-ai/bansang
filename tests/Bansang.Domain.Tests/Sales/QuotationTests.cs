using Bansang.Domain.Catalog;
using Bansang.Domain.Common;
using Bansang.Domain.Sales;

namespace Bansang.Domain.Tests.Sales;

public class QuotationTests
{
    private static readonly DateOnly Today = new(2026, 9, 26);

    [Fact]
    public void Converts_to_draft_order_keeping_quoted_prices()
    {
        var sku = TestData.Nail();
        sku.SetPrice("kg", CustomerTier.Retail, 0, 55);
        var q = new Quotation("QT-1", null, CustomerTier.Contractor, Today.AddDays(7), 10, 7, "sales", DateTimeOffset.UtcNow);
        q.AddLine(sku, "kg", 100, price: 48);
        q.AddMiscLine("ค่าขนส่ง", "เที่ยว", 1, 300);

        sku.SetPrice("kg", CustomerTier.Retail, 0, 60);             // ราคาขึ้นหลังเสนอ
        var order = q.ConvertToOrder(Guid.NewGuid(), FulfillmentMode.Delivery, new Dictionary<Guid, Sku> { [sku.Id] = sku },
            Today, "sales", DateTimeOffset.UtcNow);

        Assert.Equal(QuotationStatus.Converted, q.Status);
        Assert.Equal(order.Id, q.SalesOrderId);
        Assert.Equal(SalesOrderStatus.Draft, order.Status);
        Assert.Equal(48m, order.Lines[0].UnitPrice);
        Assert.Equal(PriceSource.Quotation, order.Lines[0].PriceSource);
        Assert.Equal(q.Total, order.Total);
        Assert.Throws<DomainException>(() => q.Cancel());
    }

    [Fact]
    public void Expired_quotation_cannot_be_converted()
    {
        var q = new Quotation("QT-1", null, CustomerTier.Retail, Today, 0, 7, "s", DateTimeOffset.UtcNow);
        q.AddMiscLine("x", "ชิ้น", 1, 10);
        var ex = Assert.Throws<DomainException>(() =>
            q.ConvertToOrder(Guid.NewGuid(), FulfillmentMode.PickupNow, new Dictionary<Guid, Sku>(), Today.AddDays(1), "s", DateTimeOffset.UtcNow));
        Assert.Equal("quotation_expired", ex.Code);
    }
}
