using Bansang.Domain.Catalog;
using Bansang.Domain.Common;

namespace Bansang.Domain.Sales;

public class Customer
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Phone { get; private set; }
    public string? TaxId { get; private set; }
    public string? Address { get; private set; }
    public CustomerTier Tier { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Customer() { }

    public Customer(string code, string name, CustomerTier tier, string? phone = null, string? taxId = null, string? address = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("code_required", "ต้องระบุรหัสลูกค้า");
        Id = Guid.CreateVersion7();
        Code = code.Trim();
        Update(name, tier, phone, taxId, address, true);
    }

    public void Update(string name, CustomerTier tier, string? phone, string? taxId, string? address, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("name_required", "ต้องระบุชื่อลูกค้า");
        Name = name.Trim();
        Tier = tier;
        Phone = Clean(phone);
        TaxId = Clean(taxId);
        Address = Clean(address);
        IsActive = isActive;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
