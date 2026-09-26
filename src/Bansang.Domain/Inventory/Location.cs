using Bansang.Domain.Common;

namespace Bansang.Domain.Inventory;

/// <summary>ที่เก็บของ: หน้าร้าน / โกดัง / ลานกอง</summary>
public class Location
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;

    private Location() { }

    public Location(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("code_required", "ต้องระบุรหัสที่เก็บ");
        Id = Guid.CreateVersion7();
        Code = code.Trim();
        Update(name, true);
    }

    public void Update(string name, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("name_required", "ต้องระบุชื่อที่เก็บ");
        Name = name.Trim();
        IsActive = isActive;
    }
}
