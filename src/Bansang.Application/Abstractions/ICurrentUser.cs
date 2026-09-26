namespace Bansang.Application.Abstractions;

/// <summary>ผู้ใช้ปัจจุบัน สำหรับ audit trail (ยังไม่มี auth จริง — อ่านจาก header X-User)</summary>
public interface ICurrentUser
{
    string UserId { get; }
}
