using Bansang.Application.Catalog;
using Bansang.Application.Inventory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bansang.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<InventoryOptions>(config.GetSection(InventoryOptions.Section));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CatalogService>();
        services.AddScoped<StockService>();
        services.AddScoped<CountService>();
        return services;
    }
}
