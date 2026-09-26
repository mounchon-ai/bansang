namespace Bansang.Domain.Catalog;

/// <summary>รูปแบบการติดตามสต็อก (A–G ตาม System Design)</summary>
public enum TrackingPattern
{
    /// <summary>A. หน่วยเดียว เช่น ทรายถุง ปูนถุง</summary>
    SingleUnit,
    /// <summary>B. แพ็กแน่นอน เช่น น็อตกล่อง 100 ตัว</summary>
    ExactPack,
    /// <summary>C. แพ็ก + ชั่ง + ประมาณ เช่น ตะปู</summary>
    PackWeighApprox,
    /// <summary>D. ขายตามความยาว เช่น สายไฟ</summary>
    Length,
    /// <summary>E. ราคาอิงน้ำหนัก/ตลาด เช่น เหล็กเส้น</summary>
    WeightPriced,
    /// <summary>F. สั่งทำ/สั่งตัด — ไม่เก็บสต็อก</summary>
    MadeToOrder,
    /// <summary>G. เทกอง เช่น หิน ทราย (คิว)</summary>
    Bulk,
}

/// <summary>สภาพของ — แยกกองสต็อก</summary>
public enum StockForm
{
    /// <summary>ปิดผนึก ขายยกลังได้</summary>
    Sealed,
    /// <summary>แกะแล้ว / แบ่งขาย</summary>
    Loose,
}

public enum CustomerTier
{
    Retail,
    Technician,
    Contractor,
}
