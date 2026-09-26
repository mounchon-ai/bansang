using System.Text.Json.Serialization;
using Bansang.Api.Endpoints;
using Bansang.Api.Infrastructure;
using Bansang.Application;
using Bansang.Application.Abstractions;
using Bansang.Infrastructure;
using Bansang.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
    if (origins.Length == 0) p.AllowAnyOrigin();
    else p.WithOrigins(origins);
    p.AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).ExcludeFromDescription();

app.MapCatalog();
app.MapInventory();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true) || app.Configuration.GetValue("Database:Seed", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (app.Configuration.GetValue("Database:MigrateOnStartup", true)) await db.Database.MigrateAsync();
    if (app.Configuration.GetValue("Database:Seed", false)) await DevSeeder.SeedAsync(db);
}

app.Run();

public partial class Program;
