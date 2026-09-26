using Bansang.Application.Catalog;
using Bansang.Application.Inventory;
using Bansang.Application.Sales;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bansang.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<InventoryOptions>(config.GetSection(InventoryOptions.Section));
        services.Configure<StoreOptions>(config.GetSection(StoreOptions.Section));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CatalogService>();
        services.AddScoped<StockService>();
        services.AddScoped<CountService>();
        services.AddScoped<DocumentNumbers>();
        services.AddScoped<CustomerService>();
        services.AddScoped<SalesService>();
        services.AddScoped<QuotationService>();
        return services;
    }
}
