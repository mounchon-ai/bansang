namespace Bansang.Domain.Sales;

/// <summary>ลูกค้ารับของยังไง</summary>
public enum FulfillmentMode
{
    /// <summary>รับเลย — ตัดสต็อกทันทีตอนยืนยันบิล</summary>
    PickupNow,
    /// <summary>ฝากไว้ ทยอยมารับ — จองไว้ ตัดเมื่อมารับแต่ละรอบ</summary>
    Deposit,
    /// <summary>ให้ร้านส่ง — จองไว้ ตัดเมื่อรถออก</summary>
    Delivery,
}

/// <summary>สถานะบิลขาย (section 9) — ยังไม่มีเครดิต จ่ายครบตอนยืนยัน รับครบ = ปิดบิล</summary>
public enum SalesOrderStatus
{
    Draft,
    Confirmed,
    PartiallyFulfilled,
    Closed,
    Cancelled,
}

public enum PaymentMethod
{
    Cash,
    Transfer,
    QR,
    Card,
}

public enum PriceSource
{
    /// <summary>จากตารางราคา (หน่วย × ระดับลูกค้า × จำนวน)</summary>
    Rule,
    /// <summary>พนักงานกรอกเอง</summary>
    Manual,
    /// <summary>ตามใบเสนอราคา</summary>
    Quotation,
}

public enum QuotationStatus
{
    Open,
    Converted,
    Cancelled,
}
