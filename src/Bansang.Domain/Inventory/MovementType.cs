namespace Bansang.Domain.Inventory;

public enum MovementType
{
    /// <summary>รับของเข้า</summary>
    Receive,
    /// <summary>ของออกจากร้าน (ขาย / ลูกค้ามารับ / รถออก)</summary>
    Issue,
    /// <summary>แกะลัง: ฝั่งออกจาก Sealed</summary>
    BreakBulkOut,
    /// <summary>แกะลัง: ฝั่งเข้า Loose</summary>
    BreakBulkIn,
    /// <summary>โอนออก (เช่น โกดัง → หน้าร้าน)</summary>
    TransferOut,
    /// <summary>โอนเข้า</summary>
    TransferIn,
    /// <summary>ปรับยอดจากการตรวจนับ / ปรับด้วยมือ</summary>
    Adjust,
    /// <summary>ลูกค้าคืนของ</summary>
    Return,
    /// <summary>รายการกลับ (แก้รายการที่บันทึกผิด)</summary>
    Reversal,
}
