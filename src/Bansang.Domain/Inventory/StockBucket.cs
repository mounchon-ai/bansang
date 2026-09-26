using Bansang.Domain.Catalog;
using Bansang.Domain.Common;

namespace Bansang.Domain.Inventory;

/// <summary>
/// กองสต็อก = SKU × ที่เก็บ × สภาพ (Sealed/Loose) — ยอดเก็บเป็นหน่วยฐานเสมอ
/// ห้ามแก้ตัวเลขตรง ๆ ทุกการเปลี่ยนยอดต้องผ่าน <see cref="Post"/> ซึ่งออก StockMovement
/// </summary>
public class StockBucket
{
    public Guid Id { get; private set; }
    public Guid SkuId { get; private set; }
    public Guid LocationId { get; private set; }
    public StockForm Form { get; private set; }
    public decimal QtyOnHandBase { get; private set; }
    public decimal QtyReservedBase { get; private set; }
    /// <summary>ติดธงให้ตรวจนับ (เช่น เคยขายติดลบ)</summary>
    public bool NeedsCount { get; private set; }

    public decimal QtyAvailableBase => QtyOnHandBase - QtyReservedBase;

    private StockBucket() { }

    public StockBucket(Guid skuId, Guid locationId, StockForm form)
    {
        Id = Guid.CreateVersion7();
        SkuId = skuId;
        LocationId = locationId;
        Form = form;
    }

    /// <summary>
    /// ลงรายการเคลื่อนไหว 1 บรรทัด แล้วอัปเดตยอด
    /// consumeReserved = ตัดของที่จองไว้แล้ว (ลูกค้ามารับของที่ซื้อฝากไว้)
    /// </summary>
    public StockMovement Post(MovementType type, decimal qtyBase, MovementContext ctx,
        bool allowNegative = false, bool consumeReserved = false)
    {
        qtyBase = Quantity.Round(qtyBase);
        if (qtyBase == 0) throw new DomainException("qty_zero", "จำนวนเคลื่อนไหวต้องไม่เป็น 0");

        if (qtyBase < 0)
        {
            var outQty = -qtyBase;
            if (consumeReserved)
            {
                if (outQty > QtyReservedBase)
                    throw new DomainException("reservation_exceeded",
                        $"ตัดของที่จองได้ไม่เกินที่จองไว้ ({QtyReservedBase}) ขอตัด {outQty}");
                QtyReservedBase -= outQty;
            }
            else
            {
                EnsureAvailable(outQty, allowNegative);
            }
        }

        QtyOnHandBase += qtyBase;
        if (QtyOnHandBase < 0 || QtyAvailableBase < 0) NeedsCount = true;
        return new StockMovement(this, type, qtyBase, QtyOnHandBase, ctx);
    }

    /// <summary>จองของ (ขายแล้วแต่ลูกค้ายังไม่รับ) — ห้ามขายซ้ำให้คนอื่น</summary>
    public void Reserve(decimal qtyBase, bool allowNegative = false)
    {
        qtyBase = Quantity.Round(qtyBase);
        Quantity.EnsurePositive(qtyBase, "จำนวนที่จอง");
        EnsureAvailable(qtyBase, allowNegative);
        QtyReservedBase += qtyBase;
        if (QtyAvailableBase < 0) NeedsCount = true;
    }

    public void Release(decimal qtyBase)
    {
        qtyBase = Quantity.Round(qtyBase);
        Quantity.EnsurePositive(qtyBase, "จำนวนที่ยกเลิกจอง");
        if (qtyBase > QtyReservedBase)
            throw new DomainException("reservation_exceeded", $"ยกเลิกจองได้ไม่เกินที่จองไว้ ({QtyReservedBase})");
        QtyReservedBase -= qtyBase;
    }

    public void ClearCountFlag() => NeedsCount = false;

    public void FlagForCount() => NeedsCount = true;

    private void EnsureAvailable(decimal qtyBase, bool allowNegative)
    {
        if (qtyBase <= QtyAvailableBase || allowNegative) return;
        var shortage = qtyBase - Math.Max(QtyAvailableBase, 0);
        throw new InsufficientStockException(
            $"สต็อกกอง {Form} ไม่พอ: พร้อมขาย {QtyAvailableBase} ต้องการ {qtyBase} (หน่วยฐาน)", shortage);
    }
}
