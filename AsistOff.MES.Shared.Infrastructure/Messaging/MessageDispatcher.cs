using AsistOff.MES.Shared.Abstractions.Messaging;
using AsistOff.MES.Shared.Infrastructure.Messaging.Interfaces;

namespace AsistOff.MES.Shared.Infrastructure.Messaging;

public class MessageDispatcher : IMessageDispatcher
{
    private readonly IMessageChannel _messageChannel;

    public MessageDispatcher(IMessageChannel messageChannel)
        => _messageChannel = messageChannel;

    public async Task PublishAsync<TMessage>(TMessage message) where TMessage : class, IMessage
        => await _messageChannel.Writer.WriteAsync(message);
}