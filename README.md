# bansang — ระบบหน้าร้าน + หลังบ้าน ร้านวัสดุก่อสร้าง

แกนระบบ (Domain + REST API) ที่แก้ปัญหา **"ของชิ้นเดียว ขายได้หลายหน่วย"**
เช่น ตะปูซื้อเข้าเป็นลัง แล้วขายเป็นลัง / กิโล / ถุง / เป็นตัว (แบบประมาณ)

> รอบนี้ครอบคลุม **Catalog + Inventory** (Phase 1 ส่วนแกน) — ยังไม่มี UI, Sales order เต็ม, เครดิต, Auth

## แนวคิดหลัก 4 ข้อ

| แนวคิด | ในโค้ด |
|---|---|
| ① **หน่วยฐาน** — ทุก SKU เก็บสต็อกเป็นหน่วยเดียวที่นับ/ชั่งได้แม่นสุด | `Sku.BaseUnit`, หน่วยฐานมี factor = 1 เสมอ |
| ② **สูตรแปลง 2 ชนิด** — แน่นอน (1 ลัง = 25 kg) / ประมาณ (1 kg ≈ 170 ตัว) | `SkuUnit.FactorToBase`, `IsExact`, `UnitConverter` |
| ③ **Stock Bucket** — SKU × ที่เก็บ × สภาพ (`Sealed` ปิดผนึก / `Loose` แบ่งขาย) | `StockBucket` |
| ④ **Stock Ledger** — ไม่แก้ยอดตรง ๆ ทุกการเปลี่ยนคือ 1 บรรทัด, ผิดให้ออกรายการกลับ | `StockMovement` (immutable), `/reverse` |

กติกาที่ระบบบังคับ:
- ทุก movement เก็บ **factor ณ วันนั้น** — แก้สูตรทีหลังประวัติไม่เพี้ยน
- **แกะลัง** ต้องสมดุล (Sealed −25 / Loose +25) และต้นทุนต่อหน่วยฐานไม่เปลี่ยน
- **จอง ≠ ตัด**: ของที่ขายแล้วแต่ลูกค้าฝากไว้ถูกจอง ห้ามขายซ้ำ; ตัดจริงตอนของออก (`fromReserved`)
- ปริมาณเก็บทศนิยม 3 ตำแหน่ง ปัดแบบ AwayFromZero จุดเดียว (`Quantity.Round`)
- การเปลี่ยนสต็อกของ SKU เดียวกัน **ล็อกแถว SKU** (`SELECT … FOR UPDATE`) กันสองเครื่อง POS ขายของชิ้นสุดท้ายพร้อมกัน
- ยอดในกอง = ผลรวม ledger เสมอ ตรวจได้ที่ `GET /api/stock/reconcile`

## โครงสร้าง

```
src/
  Bansang.Domain/          entity + กฎธุรกิจ (ไม่พึ่ง DB)
  Bansang.Application/     use case: CatalogService, StockService, CountService
  Bansang.Infrastructure/  EF Core + PostgreSQL, migrations, ข้อมูลตัวอย่าง
  Bansang.Api/             ASP.NET Core Minimal API + OpenAPI (Scalar)
tests/
  Bansang.Domain.Tests/            unit tests
  Bansang.Api.IntegrationTests/    ยิง API จริงบน PostgreSQL จริง
```

## รันบนเครื่อง server ในร้าน (Docker)

```bash
DB_PASSWORD=รหัสที่ปลอดภัย SEED_DEMO_DATA=true docker compose up -d --build
```

เปิด `http://<ip-เครื่อง>:8080/scalar` เพื่อดูและลองเรียก API ทุกตัว

ค่าที่ตั้งได้ (environment):

| ตัวแปร | ค่าเริ่มต้น | ความหมาย |
|---|---|---|
| `ALLOW_NEGATIVE_STOCK` | `false` | อนุญาตขายติดลบ (ขายได้ + ติดธงให้นับ) |
| `COUNT_THRESHOLD_PERCENT` | `3` | ส่วนต่างตอนนับไม่เกินนี้ปรับยอดอัตโนมัติ |
| `SEED_DEMO_DATA` | `false` | ใส่สินค้าตัวอย่าง (ตะปู, ทรายถุง, น็อต, สายไฟ) เมื่อฐานข้อมูลว่าง |

## พัฒนาบนเครื่อง

ต้องมี .NET 10 SDK และ PostgreSQL 16

```bash
# แก้ ConnectionStrings:Bansang ใน src/Bansang.Api/appsettings.json ถ้าจำเป็น
dotnet run --project src/Bansang.Api          # Development จะ migrate + seed ให้เอง

dotnet test tests/Bansang.Domain.Tests
# integration test: ใช้ Postgres ที่มีอยู่ หรือเว้นว่างให้ Testcontainers เปิดให้ (ต้องมี Docker)
BANSANG_TEST_PG="Host=localhost;Username=postgres;Password=postgres" dotnet test

# เพิ่ม migration
dotnet tool restore
dotnet ef migrations add <ชื่อ> -p src/Bansang.Infrastructure -s src/Bansang.Api -o Persistence/Migrations
```

ผู้ใช้สำหรับ audit trail ส่งมาทาง header `X-User` ไปก่อน (ยังไม่มีระบบ login)

## API หลัก

| งาน | Endpoint |
|---|---|
| สินค้า / SKU / หน่วย / ราคา | `/api/products`, `/api/skus`, `/api/skus/{id}/units`, `PUT /api/skus/{id}/prices` |
| สแกนบาร์โค้ด | `GET /api/skus/by-barcode/{code}` → SKU + หน่วยของบาร์โค้ดนั้น |
| คิดราคา | `GET /api/skus/{id}/price?unit=kg&tier=Technician&qty=3` |
| ยอดคงเหลือ (ทุกหน่วย) | `GET /api/stock?skuId=&locationId=` |
| เช็คของพอไหม / ต้องแกะกี่ลัง | `GET /api/stock/availability?skuId=&locationId=&unit=kg&qty=3` |
| รับของ / ตัดสต็อก / จอง / ยกเลิกจอง | `POST /api/stock/receive`, `/issue`, `/reserve`, `/release` |
| แกะลัง / โอน / ปรับยอด | `POST /api/stock/break-bulk`, `/transfer`, `/adjust` |
| ledger ย้อนหลัง / กลับรายการ | `GET /api/stock/movements`, `POST /api/stock/movements/{id}/reverse` |
| ตรวจนับ | `POST /api/counts` → `/lines` → `/submit` → `/lines/{id}/approve` |
| รายการที่ควรนับวันนี้ | `GET /api/counts/suggestions?locationId=` |
| สถิติส่วนต่าง + แนะนำแก้สูตร | `GET /api/skus/{id}/variance-stats` |

Error ตอบเป็น ProblemDetails พร้อม `code` ให้ UI ใช้ เช่น `insufficient_stock` (409), `reason_required` (422)

### ตัวอย่าง: ขายตะปู 3 kg แต่หน้าร้านเหลือ 1.2 kg

```http
POST /api/stock/issue
{ "skuId": "…", "locationId": "…", "unit": "kg", "qty": 3, "refDocument": "INV-001" }
```

ระบบแกะลัง 1 ลัง แล้วตัดสต็อกใน transaction เดียว ได้ 3 บรรทัดใน ledger:
`BreakBulkOut −25` · `BreakBulkIn +25` · `Issue −3` (ส่ง `"autoBreakBulk": false` ถ้าต้องการให้พนักงานยืนยันก่อน)

### ตรวจนับ ("ปิดลิ้นชักตอนเย็น")

1. บันทึกยอดนับจริง ระบบ snapshot ยอดในระบบ ณ ตอนนั้น
2. ส่งรอบนับ ถ้าส่วนต่างไม่เกิน 3% ระบบปรับยอดอัตโนมัติ (เหตุผล "ส่วนต่างการแบ่งขาย") ถ้าเกินจะรอเจ้าของอนุมัติพร้อมใส่สาเหตุ
3. ถ้าขายเป็นตัวแล้วนับได้ส่วนต่างไปทางเดียวกันติดกัน 3 รอบ ระบบแนะนำให้แก้สูตร เช่น 170 → 165 ตัว/kg
   (สมมติว่าส่วนต่างทั้งหมดมาจากการขายเป็นตัว: factor ใหม่ = (ที่บันทึกตัด − ส่วนต่าง) ÷ จำนวนตัวที่ขาย)

## ยังไม่ทำ (รอบถัดไป)

Sales order / POS flow เต็ม, ใบเสนอราคา, คืนของ, เครดิตและลูกหนี้, จัดส่ง, ใบสั่งซื้อ, Hangfire (สุ่มนับ / เตือนของใกล้หมด), LINE OA, Login + Role, SPA

ข้อจำกัดที่รู้อยู่:
- กลับรายการรับของ ระบบไม่คำนวณต้นทุนเฉลี่ยย้อนหลัง
- กลับรายการตัดของที่จองไว้ ของจะคืนเข้ายอดคงเหลือ แต่ไม่คืนสถานะจอง
