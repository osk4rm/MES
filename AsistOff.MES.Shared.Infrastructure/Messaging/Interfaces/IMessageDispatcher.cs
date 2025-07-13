using AsistOff.MES.Shared.Abstractions.Messaging;

namespace AsistOff.MES.Shared.Infrastructure.Messaging.Interfaces;

public interface IMessageDispatcher
{
    Task PublishAsync<TMessage>(TMessage message) where TMessage : class, IMessage;
}