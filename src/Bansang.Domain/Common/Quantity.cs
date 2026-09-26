namespace Bansang.Domain.Common;

/// <summary>
/// กติกาปัดเศษจุดเดียวของระบบ: ปริมาณในหน่วยฐานเก็บทศนิยม 3 ตำแหน่ง,
/// ต้นทุนต่อหน่วยฐานเก็บ 4 ตำแหน่ง, ปัดแบบ AwayFromZero
/// </summary>
public static class Quantity
{
    public const int QtyDecimals = 3;
    public const int CostDecimals = 4;
    public const int MoneyDecimals = 2;

    public static decimal Round(decimal qty) => Math.Round(qty, QtyDecimals, MidpointRounding.AwayFromZero);
    public static decimal RoundCost(decimal cost) => Math.Round(cost, CostDecimals, MidpointRounding.AwayFromZero);
    public static decimal RoundMoney(decimal amount) => Math.Round(amount, MoneyDecimals, MidpointRounding.AwayFromZero);

    public static void EnsurePositive(decimal qty, string what = "จำนวน")
    {
        if (qty <= 0) throw new DomainException("qty_not_positive", $"{what}ต้องมากกว่า 0");
    }
}
