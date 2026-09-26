using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bansang.Api.IntegrationTests;

/// <summary>
/// รัน API จริงบน PostgreSQL จริง — ใช้ connection string จาก BANSANG_TEST_PG ถ้ามี
/// ไม่งั้นเปิด Postgres ด้วย Testcontainers (ต้องมี Docker) แต่ละ fixture ได้ database ของตัวเอง
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string _connectionString = null!;

    protected virtual bool AllowNegativeStock => false;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable("BANSANG_TEST_PG");
        if (string.IsNullOrWhiteSpace(server))
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await _container.StartAsync();
            server = _container.GetConnectionString();
        }
        _connectionString = new NpgsqlConnectionStringBuilder(server) { Database = $"bansang_test_{Guid.NewGuid():N}" }
            .ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Bansang", _connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Database:Seed", "false");
        builder.UseSetting("Inventory:AllowNegativeStock", AllowNegativeStock.ToString());
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }
        var csb = new NpgsqlConnectionStringBuilder(_connectionString);
        var db = csb.Database;
        csb.Database = "postgres";
        await using var conn = new NpgsqlConnection(csb.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{db}\" WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public HttpClient Client(string user = "tester")
    {
        var c = CreateClient();
        c.DefaultRequestHeaders.Add("X-User", user);
        return c;
    }
}

public sealed class NegativeStockApiFactory : ApiFactory
{
    protected override bool AllowNegativeStock => true;
}

public static class HttpExtensions
{
    public static async Task<T> PostOk<T>(this HttpClient c, string url, object body)
    {
        var res = await c.PostAsJsonAsync(url, body, ApiFactory.Json);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"POST {url} → {(int)res.StatusCode}: {text}");
        return JsonSerializer.Deserialize<T>(text, ApiFactory.Json)!;
    }

    public static async Task<T> PutOk<T>(this HttpClient c, string url, object body)
    {
        var res = await c.PutAsJsonAsync(url, body, ApiFactory.Json);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"PUT {url} → {(int)res.StatusCode}: {text}");
        return JsonSerializer.Deserialize<T>(text, ApiFactory.Json)!;
    }

    public static async Task<T> GetOk<T>(this HttpClient c, string url)
    {
        var res = await c.GetAsync(url);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"GET {url} → {(int)res.StatusCode}: {text}");
        return JsonSerializer.Deserialize<T>(text, ApiFactory.Json)!;
    }

    public static async Task<(int Status, string? Code)> PostFail(this HttpClient c, string url, object body)
    {
        var res = await c.PostAsJsonAsync(url, body, ApiFactory.Json);
        Assert.False(res.IsSuccessStatusCode, $"POST {url} ควรล้มเหลวแต่ได้ {(int)res.StatusCode}");
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return ((int)res.StatusCode, doc.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null);
    }
}
