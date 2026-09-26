using Bansang.Domain.Catalog;

namespace Bansang.Application.Contracts;

public record ProductDto(Guid Id, string Name, string? Category);
public record SaveProductRequest(string Name, string? Category);

/// <summary>
/// ระบุสูตรแปลงได้ 2 แบบ: FactorToBase (1 ลัง = 25 kg → 25)
/// หรือ UnitsPerBase (1 kg ≈ 170 ตัว → 170) อย่างใดอย่างหนึ่ง
/// </summary>
public record SkuUnitInput(
    string UnitName,
    decimal? FactorToBase,
    decimal? UnitsPerBase,
    bool IsExact,
    StockForm Form,
    string? Barcode,
    bool CanSell = true,
    bool CanBuy = true);

public record UpdateSkuUnitRequest(StockForm Form, string? Barcode, bool CanSell, bool CanBuy);

public record ChangeFactorRequest(decimal? FactorToBase, decimal? UnitsPerBase, bool IsExact);

public record PriceInput(string UnitName, CustomerTier Tier, decimal MinQty, decimal Price);

public record CreateSkuRequest(
    Guid ProductId,
    string Code,
    string Name,
    string? Spec,
    string BaseUnit,
    TrackingPattern TrackingPattern,
    StockForm BaseForm = StockForm.Loose,
    string? BaseBarcode = null,
    IReadOnlyList<SkuUnitInput>? Units = null,
    IReadOnlyList<PriceInput>? Prices = null);

public record UpdateSkuRequest(string Name, string? Spec, TrackingPattern TrackingPattern, bool IsActive);

public record SkuUnitDto(
    string UnitName, decimal FactorToBase, decimal UnitsPerBase, bool IsExact, StockForm Form,
    string? Barcode, bool CanSell, bool CanBuy, bool IsBase);

public record PriceRuleDto(string UnitName, CustomerTier Tier, decimal MinQty, decimal Price);

public record SkuDto(
    Guid Id, Guid ProductId, string Code, string Name, string? Spec, string BaseUnit,
    TrackingPattern TrackingPattern, decimal AvgCostPerBase, bool IsActive,
    IReadOnlyList<SkuUnitDto> Units, IReadOnlyList<PriceRuleDto> Prices);

public record SkuListItemDto(Guid Id, string Code, string Name, string? Spec, string BaseUnit,
    TrackingPattern TrackingPattern, bool IsActive, string ProductName);

public record SkuSearchQuery(int Page = 1, int PageSize = 20, string? Search = null,
    Guid? ProductId = null, TrackingPattern? TrackingPattern = null, bool? IsActive = null)
    : PageQuery(Page, PageSize, Search);

public record BarcodeLookupDto(SkuDto Sku, SkuUnitDto Unit);

public record PriceQuoteDto(Guid SkuId, string UnitName, CustomerTier RequestedTier, CustomerTier AppliedTier,
    decimal Qty, decimal MinQty, decimal UnitPrice, decimal Total);

public static class CatalogMapping
{
    public static ProductDto ToDto(this Product p) => new(p.Id, p.Name, p.Category);

    public static SkuUnitDto ToDto(this SkuUnit u) => new(u.UnitName, u.FactorToBase, Math.Round(u.UnitsPerBase, 6),
        u.IsExact, u.Form, u.Barcode, u.CanSell, u.CanBuy, u.IsBase);

    public static SkuDto ToDto(this Sku s) => new(s.Id, s.ProductId, s.Code, s.Name, s.Spec, s.BaseUnit,
        s.TrackingPattern, s.AvgCostPerBase, s.IsActive,
        s.Units.OrderByDescending(u => u.IsBase).ThenByDescending(u => u.FactorToBase).Select(u => u.ToDto()).ToList(),
        s.PriceRules.OrderBy(p => p.UnitName).ThenBy(p => p.Tier).ThenBy(p => p.MinQty)
            .Select(p => new PriceRuleDto(p.UnitName, p.Tier, p.MinQty, p.Price)).ToList());
}
