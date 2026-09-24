using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Production.Infrastructure.Configurations;
using AsistOff.MES.Production.Infrastructure.Repositories;
using AsistOff.MES.Shared.Abstractions.DAL;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Production.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProductionInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IRecipesRepository, RecipesRepository>();
        services.AddScoped<IRecipeVersionsRepository, RecipeVersionsRepository>();
        services.AddScoped<IOperationNodesRepository, OperationNodesRepository>();
        services.AddScoped<IChildEntitiesRepository, ChildEntitiesRepository>();
        services.AddScoped<IOperationDependenciesRepository, OperationDependenciesRepository>();
        services.AddScoped<IOperationTemplatesRepository, OperationTemplatesRepository>();
        services.AddScoped<IProductionOrdersRepository, ProductionOrdersRepository>();
        services.AddScoped<ISpcCharacteristicsRepository, SpcCharacteristicsRepository>();
        services.AddScoped<IDowntimeEventsRepository, DowntimeEventsRepository>();
        services.AddScoped<IAndonSignalsRepository, AndonSignalsRepository>();
        services.AddScoped<ILotsRepository, LotsRepository>();
        services.AddScoped<IScrapEventsRepository, ScrapEventsRepository>();
        services.AddScoped<IMachineTelemetryTagsRepository, MachineTelemetryTagsRepository>();
        services.AddScoped<ITelemetryReadingsRepository, TelemetryReadingsRepository>();

        services.AddScoped<IEntityConfigurator, ProductionEntityConfigurator>();

        return services;
    }
}
