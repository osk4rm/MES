using AsistOff.MES.Shared.Abstractions.Events;
using AsistOff.MES.Shared.Abstractions.Messaging;
using AsistOff.MES.Shared.Infrastructure.Events;
using AsistOff.MES.Shared.Infrastructure.Messaging.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AsistOff.MES.Shared.Infrastructure.Messaging;

internal static class Extensions
{
    private const string SectionName = "messaging";
    
    internal static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        services.AddSingleton<IMessageBroker, MessageBroker>();
        services.AddSingleton<IMessageChannel, MessageChannel>();
        services.AddSingleton<IMessageDispatcher, MessageDispatcher>();

        var messagingOptions = services.GetOptions<MessagingOptions>(SectionName);
        services.AddSingleton(messagingOptions);

        if (messagingOptions.UseBackgroundDispatcher)
        {
            services.AddHostedService<BackgroundDispatcher>();
        }
            
        return services;
    }
}