using Bansang.Domain.Common;

namespace Bansang.Domain.Catalog;

public class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Category { get; private set; }

    private Product() { }

    public Product(string name, string? category)
    {
        Id = Guid.CreateVersion7();
        Rename(name, category);
    }

    public void Rename(string name, string? category)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("name_required", "ต้องระบุชื่อสินค้า");
        Name = name.Trim();
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
    }
}
