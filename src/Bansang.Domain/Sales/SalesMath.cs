using Bansang.Domain.Common;

namespace Bansang.Domain.Sales;

public static class SalesMath
{
    public static decimal LineTotal(decimal qty, decimal unitPrice, decimal discount)
        => Quantity.RoundMoney(qty * unitPrice) - discount;

    /// <summary>ราคารวม VAT แล้ว → ถอด VAT ออก: total × rate / (100 + rate)</summary>
    public static decimal VatIncluded(decimal total, decimal ratePercent)
        => ratePercent <= 0 ? 0 : Quantity.RoundMoney(total * ratePercent / (100 + ratePercent));

    public static void ValidateLine(decimal qty, decimal unitPrice, decimal discount)
    {
        Quantity.EnsurePositive(qty);
        if (unitPrice < 0) throw new DomainException("price_negative", "ราคาต้องไม่ติดลบ");
        if (discount < 0) throw new DomainException("discount_negative", "ส่วนลดต้องไม่ติดลบ");
        if (discount > Quantity.RoundMoney(qty * unitPrice))
            throw new DomainException("discount_exceeds", "ส่วนลดเกินยอดของบรรทัด");
    }
}
