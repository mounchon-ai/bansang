using Bansang.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Bansang.Application.Sales;

/// <summary>ออกเลขที่เอกสาร เช่น INV2609-00001 (เรียงต่อเนื่องต่อเดือน)</summary>
public sealed class DocumentNumbers(IAppDbContext db, TimeProvider clock, IOptions<StoreOptions> store)
{
    public const string Invoice = "INV";
    public const string Pickup = "PU";
    public const string Return = "RT";
    public const string Quotation = "QT";
    public const string Customer = "C";

    public DateTimeOffset LocalNow => TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone);
    public DateOnly Today => DateOnly.FromDateTime(LocalNow.DateTime);

    public async Task<string> NextAsync(string prefix, CancellationToken ct)
    {
        var period = LocalNow.ToString("yyMM");
        var n = await db.NextSequenceAsync(prefix, period, ct);
        return $"{prefix}{period}-{n:D5}";
    }

    /// <summary>เลขไม่แยกงวด เช่น รหัสลูกค้า C00001</summary>
    public async Task<string> NextPlainAsync(string prefix, CancellationToken ct)
        => $"{prefix}{await db.NextSequenceAsync(prefix, "", ct):D5}";

    private TimeZoneInfo Zone
    {
        get
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(store.Value.TimeZone); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
        }
    }
}
