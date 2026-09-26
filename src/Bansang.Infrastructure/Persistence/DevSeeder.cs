using Bansang.Domain.Catalog;
using Bansang.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Bansang.Infrastructure.Persistence;

/// <summary>ข้อมูลตัวอย่างตาม System Design — ใส่เฉพาะตอนฐานข้อมูลว่าง</summary>
public static class DevSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Products.AnyAsync(ct)) return;

        db.Locations.AddRange(new Location("FRONT", "หน้าร้าน"), new Location("WH", "โกดัง"), new Location("YARD", "ลานกอง"));

        // C. ตะปู: หน่วยฐาน kg, ลัง = 25 kg, ถุง = 0.5 kg, 1 kg ≈ 170 ตัว
        var nail = new Product("ตะปู", "ฮาร์ดแวร์");
        var nail2 = new Sku(nail.Id, "NAIL-2IN", "ตะปู 2 นิ้ว", "2 นิ้ว", "kg", TrackingPattern.PackWeighApprox);
        nail2.AddUnit("ลัง", 25m, true, StockForm.Sealed, "8850000000011");
        nail2.AddUnit("ถุง", 0.5m, true, StockForm.Loose, "8850000000028");
        nail2.AddUnit("ตัว", 1m / 170m, false, StockForm.Loose);
        nail2.SetPrice("ลัง", CustomerTier.Retail, 0, 1150);
        nail2.SetPrice("kg", CustomerTier.Retail, 0, 55);
        nail2.SetPrice("kg", CustomerTier.Technician, 0, 50);
        nail2.SetPrice("ถุง", CustomerTier.Retail, 0, 30);
        nail2.SetPrice("ตัว", CustomerTier.Retail, 0, 0.5m);

        // A. ทรายถุง: หน่วยเดียว
        var sand = new Product("ทรายถุง", "วัสดุก่อ");
        var sandBag = new Sku(sand.Id, "SAND-BAG", "ทรายหยาบ ถุง", "25 kg", "ถุง", TrackingPattern.SingleUnit,
            StockForm.Loose, "8850000000035");
        sandBag.SetPrice("ถุง", CustomerTier.Retail, 0, 45);
        sandBag.SetPrice("ถุง", CustomerTier.Contractor, 50, 38);

        // B. น็อต: หน่วยฐาน ตัว, กล่อง = 100 ตัว
        var bolt = new Product("น็อตหกเหลี่ยม", "ฮาร์ดแวร์");
        var boltM8 = new Sku(bolt.Id, "BOLT-M8x25", "น็อต M8×25", "M8×25", "ตัว", TrackingPattern.ExactPack);
        boltM8.AddUnit("กล่อง", 100m, true, StockForm.Sealed, "8850000000042");
        boltM8.SetPrice("กล่อง", CustomerTier.Retail, 0, 250);
        boltM8.SetPrice("ตัว", CustomerTier.Retail, 0, 3);

        // D. สายไฟ: หน่วยฐาน เมตร, ม้วน = 100 m
        var wire = new Product("สายไฟ THW", "ไฟฟ้า");
        var wire25 = new Sku(wire.Id, "THW-2.5", "สายไฟ THW 2.5 sq.mm", "2.5 sq.mm", "เมตร", TrackingPattern.Length);
        wire25.AddUnit("ม้วน", 100m, true, StockForm.Sealed, "8850000000059");
        wire25.SetPrice("ม้วน", CustomerTier.Retail, 0, 1450);
        wire25.SetPrice("เมตร", CustomerTier.Retail, 0, 17);

        db.Products.AddRange(nail, sand, bolt, wire);
        db.Skus.AddRange(nail2, sandBag, boltM8, wire25);
        await db.SaveChangesAsync(ct);
    }
}
