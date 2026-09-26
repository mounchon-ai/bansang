using Bansang.Domain.Catalog;
using Bansang.Domain.Common;
using Bansang.Domain.Sales;

namespace Bansang.Domain.Tests.Sales;

public class SalesOrderTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Sku PricedNail()
    {
        var sku = TestData.Nail();
        sku.SetPrice("kg", CustomerTier.Retail, 0, 55);
        sku.SetPrice("kg", CustomerTier.Technician, 0, 50);
        sku.SetPrice("ลัง", CustomerTier.Retail, 0, 1150);
        sku.SetPrice("ตัว", CustomerTier.Retail, 0, 0.5m);
        return sku;
    }

    private static SalesOrder NewOrder(FulfillmentMode mode = FulfillmentMode.PickupNow, CustomerTier tier = CustomerTier.Retail)
        => new(Guid.NewGuid(), mode, null, tier, 7, "cashier", Now);

    [Fact]
    public void Multiple_units_in_one_bill_with_rule_prices_and_vat_included()
    {
        var sku = PricedNail();
        var o = NewOrder();
        var kg = o.AddLine(sku, "kg", 3);
        var box = o.AddLine(sku, "ลัง", 1);
        var pcs = o.AddLine(sku, "ตัว", 50);

        Assert.Equal(165m, kg.LineTotal);
        Assert.Equal(1150m, box.LineTotal);
        Assert.Equal(25m, pcs.LineTotal);
        Assert.Equal(1340m, o.Total);
        Assert.Equal(87.66m, o.VatAmount);            // 1340 × 7 / 107 = 87.663…
        Assert.Equal(1252.34m, o.TotalBeforeVat);
        Assert.Equal((3m, StockForm.Loose), (kg.QtyBase, kg.Form!.Value));
        Assert.Equal((25m, StockForm.Sealed), (box.QtyBase, box.Form!.Value));
        Assert.True(pcs.IsApproximate);
        Assert.Equal(0.294m, pcs.QtyBase);
    }

    [Fact]
    public void Customer_tier_reprices_rule_lines_but_not_manual_ones()
    {
        var sku = PricedNail();
        var o = NewOrder();
        var rule = o.AddLine(sku, "kg", 10);
        var manual = o.AddLine(sku, "ลัง", 1, price: 1100);
        o.ChangeCustomer(Guid.NewGuid(), CustomerTier.Technician, new Dictionary<Guid, Sku> { [sku.Id] = sku });
        Assert.Equal(50m, rule.UnitPrice);
        Assert.Equal(1100m, manual.UnitPrice);
        Assert.Equal(PriceSource.Manual, manual.PriceSource);
    }

    [Fact]
    public void Missing_price_must_be_entered_manually()
    {
        var o = NewOrder();
        var ex = Assert.Throws<DomainException>(() => o.AddLine(PricedNail(), "ถุง", 1));
        Assert.Equal("price_missing", ex.Code);
        Assert.Equal(30m, o.AddLine(PricedNail(), "ถุง", 1, price: 30).LineTotal);
    }

    [Fact]
    public void Discounts_cannot_exceed_amounts()
    {
        var o = NewOrder();
        Assert.Throws<DomainException>(() => o.AddLine(PricedNail(), "kg", 1, discount: 56));
        o.AddLine(PricedNail(), "kg", 1);
        o.Update(FulfillmentMode.PickupNow, 100, null);
        Assert.Equal("discount_exceeds", Assert.Throws<DomainException>(() =>
            o.Confirm("INV-1", [new PaymentInput(PaymentMethod.Cash, 100)], "u", Now)).Code);
    }

    [Fact]
    public void Confirm_requires_full_payment_and_change_only_from_cash()
    {
        var o = NewOrder();
        o.AddLine(PricedNail(), "kg", 3);                        // 165
        Assert.Equal("payment_insufficient", Assert.Throws<DomainException>(() =>
            o.Confirm("INV-1", [new PaymentInput(PaymentMethod.Cash, 100)], "u", Now)).Code);
        Assert.Equal("overpaid_non_cash", Assert.Throws<DomainException>(() =>
            o.Confirm("INV-1", [new PaymentInput(PaymentMethod.Transfer, 200)], "u", Now)).Code);

        o.Confirm("INV-1", [new PaymentInput(PaymentMethod.Transfer, 100), new PaymentInput(PaymentMethod.Cash, 100)], "u", Now);
        Assert.Equal(35m, o.ChangeAmount);
        Assert.Equal(165m, o.PaidAmount);
        Assert.Equal("INV-1", o.Number);
        Assert.Equal(SalesOrderStatus.Confirmed, o.Status);
        Assert.Throws<DomainException>(() => o.AddLine(PricedNail(), "kg", 1));   // แก้ไม่ได้แล้ว
    }

    [Fact]
    public void Deposit_is_picked_up_in_rounds_then_closed()
    {
        var o = NewOrder(FulfillmentMode.Deposit);
        var line = o.AddLine(PricedNail(), "kg", 10);
        var effects = o.Confirm("INV-1", [new PaymentInput(PaymentMethod.Cash, 550)], "u", Now);
        Assert.Equal(10m, Assert.Single(effects).QtyBase);

        o.Fulfil("PU-1", [new LineQty(line.Id, 4)], "u", Now);
        Assert.Equal(SalesOrderStatus.PartiallyFulfilled, o.Status);
        Assert.Equal("fulfil_exceeds", Assert.Throws<DomainException>(() =>
            o.Fulfil("PU-2", [new LineQty(line.Id, 7)], "u", Now)).Code);
        o.Fulfil("PU-2", [new LineQty(line.Id, 6)], "u", Now);
        Assert.Equal(SalesOrderStatus.Closed, o.Status);
        Assert.Equal(2, o.Fulfillments.Count);
    }

    [Fact]
    public void Approximate_units_last_pickup_takes_exact_remaining_base()
    {
        var o = NewOrder(FulfillmentMode.Deposit);
        var line = o.AddLine(PricedNail(), "ตัว", 1000);          // 1000/170 = 5.882 kg
        o.Confirm("INV-1", [new PaymentInput(PaymentMethod.Cash, 500)], "u", Now);

        var total = 0m;
        foreach (var q in new[] { 333m, 333m, 334m })
            total += o.Fulfil("PU", [new LineQty(line.Id, q)], "u", Now).Effects.Single().QtyBase;

        Assert.Equal(5.882m, line.QtyBase);
        Assert.Equal(line.QtyBase, total);                     // 1.959 + 1.959 + 1.964 — ไม่เกินที่จอง
        Assert.Equal(0m, line.RemainingBase);
    }

    [Fact]
    public void Misc_items_are_delivered_on_confirm_without_stock()
    {
        var o = NewOrder(FulfillmentMode.Deposit);
        o.AddMiscLine("ค่าตัดเหล็ก", "ครั้ง", 1, 50);
        var effects = o.Confirm("INV-1", [new PaymentInput(PaymentMethod.Cash, 50)], "u", Now);
        Assert.Empty(effects);
        Assert.Equal(SalesOrderStatus.Closed, o.Status);
    }

    [Fact]
    public void Cancel_confirmed_order_refunds_net_of_change_and_releases_reservation()
    {
        var o = NewOrder(FulfillmentMode.Deposit);
        o.AddLine(PricedNail(), "kg", 3);                          // 165
        o.Confirm("INV-1", [new PaymentInput(PaymentMethod.Transfer, 100), new PaymentInput(PaymentMethod.Cash, 100)], "u", Now);
        var release = o.Cancel("ลูกค้าเปลี่ยนใจ", "owner", Now);

        Assert.Equal(3m, Assert.Single(release).QtyBase);
        Assert.Equal(SalesOrderStatus.Cancelled, o.Status);
        var refunds = o.Payments.Where(p => p.IsRefund).ToList();
        Assert.Equal(165m, refunds.Sum(p => p.Amount));
        Assert.Equal(65m, refunds.Single(p => p.Method == PaymentMethod.Cash).Amount);
    }

    [Fact]
    public void Cannot_cancel_after_pickup()
    {
        var o = NewOrder(FulfillmentMode.Deposit);
        var line = o.AddLine(PricedNail(), "kg", 3);
        o.Confirm("INV-1", [new PaymentInput(PaymentMethod.Cash, 165)], "u", Now);
        o.Fulfil("PU-1", [new LineQty(line.Id, 1)], "u", Now);
        Assert.Equal("cancel_after_pickup", Assert.Throws<DomainException>(() => o.Cancel("x", "u", Now)).Code);
    }

    [Fact]
    public void Return_only_what_was_picked_up_and_refund_with_bill_discount_allocated()
    {
        var o = NewOrder(FulfillmentMode.Deposit);
        var line = o.AddLine(PricedNail(), "kg", 10);             // 550
        o.Update(FulfillmentMode.Deposit, 55, null);                // สุทธิ 495 → 49.5/kg
        o.Confirm("INV-1", [new PaymentInput(PaymentMethod.Cash, 495)], "u", Now);
        o.Fulfil("PU-1", [new LineQty(line.Id, 4)], "u", Now);

        Assert.Equal("return_exceeds", Assert.Throws<DomainException>(() =>
            o.Return("RT-1", [new LineQty(line.Id, 5)], "ของเสีย", PaymentMethod.Cash, "u", Now)).Code);

        var (doc, effects) = o.Return("RT-1", [new LineQty(line.Id, 2)], "ของเสีย", PaymentMethod.Cash, "u", Now);
        Assert.Equal(99m, doc.RefundAmount);
        Assert.Equal(2m, Assert.Single(effects).QtyBase);
        Assert.Equal(99m, o.RefundedAmount);
        Assert.Equal(2m, line.ReturnableQty);
    }

    [Fact]
    public void Returning_everything_refunds_exactly_the_line_net()
    {
        var o = NewOrder();
        var line = o.AddLine(PricedNail(), "kg", 3, price: 33.33m); // 99.99
        o.Confirm("INV-1", [new PaymentInput(PaymentMethod.Cash, 100)], "u", Now);
        o.FulfilAll("PU-1", "u", Now);
        var r1 = o.Return("RT-1", [new LineQty(line.Id, 1)], "x", PaymentMethod.Cash, "u", Now).Doc.RefundAmount;
        var r2 = o.Return("RT-2", [new LineQty(line.Id, 2)], "x", PaymentMethod.Cash, "u", Now).Doc.RefundAmount;
        Assert.Equal(99.99m, r1 + r2);
    }
}
