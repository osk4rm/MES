using AsistOff.MES.CustomerOrders.Application;
using AsistOff.MES.CustomerOrders.Infrastructure;
using AsistOff.MES.Shared.Abstractions.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.CustomerOrders.Api;

internal sealed class CustomerOrdersModule : IModule
{
    public const string BasePath = "customer-orders";
    public string Name => "CustomerOrders";
    public string Path => BasePath;
    public IEnumerable<string> Policies => ["customer-orders"];

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddCustomerOrdersApplication();
        services.AddCustomerOrdersInfrastructure();
    }

    public void Use(IApplicationBuilder app)
    {
    }
}
