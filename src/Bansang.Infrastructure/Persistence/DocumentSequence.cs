namespace Bansang.Infrastructure.Persistence;

/// <summary>ตัวนับเลขที่เอกสารต่อ (prefix, งวด) — แก้ผ่าน SQL upsert เท่านั้น</summary>
public class DocumentSequence
{
    public string Prefix { get; set; } = null!;
    public string Period { get; set; } = null!;
    public long LastValue { get; set; }
}
