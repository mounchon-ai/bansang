using Bansang.Domain.Common;

namespace Bansang.Domain.Inventory;

public sealed record FactorSuggestion(
    int ConsecutiveSameSign,
    decimal TotalVarianceBase,
    decimal ApproxIssuedBase,
    decimal ApproxIssuedInputQty,
    decimal CurrentFactor,
    decimal SuggestedFactor)
{
    /// <summary>เช่น ≈165 ตัว/kg</summary>
    public decimal CurrentUnitsPerBase => Math.Round(1m / CurrentFactor, 2);
    public decimal SuggestedUnitsPerBase => Math.Round(1m / SuggestedFactor, 2);
}

/// <summary>
/// ถ้านับแล้วเพี้ยนไปทางเดียวกันซ้ำ ๆ แปลว่าสูตรประมาณ (เช่น 1 kg ≈ 170 ตัว) ไม่ตรงความจริง
///
/// สมมติส่วนต่างทั้งหมดมาจากการขายเป็นตัว:
///   บันทึกตัดไป R หน่วยฐาน จากการขาย N ตัว, ส่วนต่างรวม V (ติดลบ = ของหายมากกว่าที่บันทึก)
///   ของออกจริง = R − V  →  factor ใหม่ = (R − V) / N
/// </summary>
public static class FactorAdvisor
{
    public const int DefaultStreak = 3;

    /// <param name="variancesChronological">ส่วนต่างของรอบนับ (Loose) เรียงเก่า → ใหม่</param>
    public static FactorSuggestion? Suggest(IReadOnlyList<decimal> variancesChronological,
        decimal approxIssuedBase, decimal approxIssuedInputQty, decimal currentFactor, int streak = DefaultStreak)
    {
        var nonZero = variancesChronological.Where(v => v != 0).ToList();
        if (nonZero.Count < streak) return null;
        var recent = nonZero.TakeLast(streak).ToList();
        var sign = Math.Sign(recent[0]);
        if (recent.Any(v => Math.Sign(v) != sign)) return null;
        if (approxIssuedInputQty <= 0 || approxIssuedBase <= 0) return null;

        var totalVariance = recent.Sum();
        var actualOut = approxIssuedBase - totalVariance;
        if (actualOut <= 0) return null;

        var suggested = Math.Round(actualOut / approxIssuedInputQty, 12, MidpointRounding.AwayFromZero);
        return new FactorSuggestion(streak, Quantity.Round(totalVariance), approxIssuedBase, approxIssuedInputQty,
            currentFactor, suggested);
    }
}
