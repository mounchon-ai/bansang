namespace Bansang.Domain.Common;

/// <summary>ละเมิดกฎทางธุรกิจ (invariant) — API แปลงเป็น 422</summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>สต็อกไม่พอ — API แปลงเป็น 409</summary>
public sealed class InsufficientStockException(string message, decimal shortageBase)
    : DomainException("insufficient_stock", message)
{
    public decimal ShortageBase { get; } = shortageBase;
}
