namespace Bansang.Application;

/// <summary>ข้อมูลร้านสำหรับหัวใบเสร็จ + ภาษี</summary>
public sealed class StoreOptions
{
    public const string Section = "Store";

    public string Name { get; set; } = "ร้านวัสดุก่อสร้าง";
    public string? Branch { get; set; } = "สำนักงานใหญ่";
    public string? Address { get; set; }
    public string? TaxId { get; set; }
    public string? Phone { get; set; }

    /// <summary>อัตรา VAT (%) — ราคาขายรวม VAT แล้ว; 0 = ร้านไม่ได้จด VAT</summary>
    public decimal VatRatePercent { get; set; } = 7m;

    /// <summary>เขตเวลาสำหรับงวดเลขที่เอกสาร (yyMM)</summary>
    public string TimeZone { get; set; } = "Asia/Bangkok";

    public int QuotationValidDays { get; set; } = 30;
}
