namespace Bansang.Application;

public sealed class InventoryOptions
{
    public const string Section = "Inventory";

    /// <summary>ร้านอนุญาตขายติดลบ (ขายได้ + ติดธงให้ตรวจนับ)</summary>
    public bool AllowNegativeStock { get; set; }

    /// <summary>เกณฑ์ส่วนต่างตอนนับ (%) — ไม่เกินนี้ปรับยอดอัตโนมัติ</summary>
    public decimal CountVarianceThresholdPercent { get; set; } = 3m;

    /// <summary>นับเพี้ยนทางเดียวกันกี่รอบติดจึงแนะนำแก้สูตร</summary>
    public int FactorSuggestionStreak { get; set; } = 3;
}
