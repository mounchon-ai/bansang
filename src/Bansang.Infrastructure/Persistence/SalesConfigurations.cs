using Bansang.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bansang.Infrastructure.Persistence;

internal sealed class DocumentSequenceConfig : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> b)
    {
        b.HasKey(x => new { x.Prefix, x.Period });
        b.Property(x => x.Prefix).HasMaxLength(10);
        b.Property(x => x.Period).HasMaxLength(10);
    }
}

internal sealed class CustomerConfig : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.TaxId).HasMaxLength(20);
        b.Property(x => x.Address).HasMaxLength(500);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.Name);
        b.HasIndex(x => x.Phone);
    }
}

internal sealed class SalesOrderConfig : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.BillDiscount).Money();
        b.Property(x => x.ChangeAmount).Money();
        b.Property(x => x.VatRatePercent).HasPrecision(5, 2);
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.CancelReason).HasMaxLength(500);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.ConfirmedBy).HasMaxLength(100);
        b.HasIndex(x => x.Number).IsUnique().HasFilter("number IS NOT NULL");
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.HasIndex(x => x.CustomerId);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Bansang.Domain.Inventory.Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Payments).WithOne().HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Fulfillments).WithOne().HasForeignKey(f => f.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Returns).WithOne().HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.Cascade);
        foreach (var nav in new[] { nameof(SalesOrder.Lines), nameof(SalesOrder.Payments), nameof(SalesOrder.Fulfillments), nameof(SalesOrder.Returns) })
            b.Navigation(nav).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SalesLineConfig : IEntityTypeConfiguration<SalesLine>
{
    public void Configure(EntityTypeBuilder<SalesLine> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.SkuCode).HasMaxLength(50);
        b.Property(x => x.Description).HasMaxLength(200);
        b.Property(x => x.UnitName).HasMaxLength(30);
        b.Property(x => x.Qty).Qty();
        b.Property(x => x.UnitPrice).Money();
        b.Property(x => x.LineDiscount).Money();
        b.Property(x => x.QtyBase).Qty();
        b.Property(x => x.FactorSnapshot).Factor();
        b.Property(x => x.FulfilledQty).Qty();
        b.Property(x => x.FulfilledBase).Qty();
        b.Property(x => x.ReturnedQty).Qty();
        b.Property(x => x.ReturnedBase).Qty();
        b.Property(x => x.RefundedAmount).Money();
        b.HasIndex(x => new { x.OrderId, x.LineNo }).IsUnique();
        b.HasIndex(x => x.SkuId);
        b.HasOne<Bansang.Domain.Catalog.Sku>().WithMany().HasForeignKey(x => x.SkuId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PaymentConfig : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Amount).Money();
        b.Property(x => x.Reference).HasMaxLength(100);
        b.Property(x => x.UserId).HasMaxLength(100);
        b.HasIndex(x => x.At);
    }
}

internal sealed class FulfillmentConfig : IEntityTypeConfiguration<Fulfillment>
{
    public void Configure(EntityTypeBuilder<Fulfillment> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.UserId).HasMaxLength(100);
        b.HasIndex(x => x.Number).IsUnique();
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.FulfillmentId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class FulfillmentLineConfig : IEntityTypeConfiguration<FulfillmentLine>
{
    public void Configure(EntityTypeBuilder<FulfillmentLine> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Qty).Qty();
        b.Property(x => x.QtyBase).Qty();
        b.HasOne<SalesLine>().WithMany().HasForeignKey(x => x.SalesLineId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SalesReturnConfig : IEntityTypeConfiguration<SalesReturn>
{
    public void Configure(EntityTypeBuilder<SalesReturn> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.RefundAmount).Money();
        b.Property(x => x.UserId).HasMaxLength(100);
        b.HasIndex(x => x.Number).IsUnique();
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.ReturnId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SalesReturnLineConfig : IEntityTypeConfiguration<SalesReturnLine>
{
    public void Configure(EntityTypeBuilder<SalesReturnLine> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Qty).Qty();
        b.Property(x => x.QtyBase).Qty();
        b.Property(x => x.Amount).Money();
        b.HasOne<SalesLine>().WithMany().HasForeignKey(x => x.SalesLineId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class QuotationConfig : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Number).HasMaxLength(30);
        b.Property(x => x.BillDiscount).Money();
        b.Property(x => x.VatRatePercent).HasPrecision(5, 2);
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.HasIndex(x => x.Number).IsUnique();
        b.HasIndex(x => x.CustomerId);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.QuotationId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class QuotationLineConfig : IEntityTypeConfiguration<QuotationLine>
{
    public void Configure(EntityTypeBuilder<QuotationLine> b)
    {
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.SkuCode).HasMaxLength(50);
        b.Property(x => x.Description).HasMaxLength(200);
        b.Property(x => x.UnitName).HasMaxLength(30);
        b.Property(x => x.Qty).Qty();
        b.Property(x => x.UnitPrice).Money();
        b.Property(x => x.LineDiscount).Money();
        b.HasOne<Bansang.Domain.Catalog.Sku>().WithMany().HasForeignKey(x => x.SkuId).OnDelete(DeleteBehavior.Restrict);
    }
}
