using AsistOff.MES.Shared.Abstractions.Messaging;
using AsistOff.MES.Shared.Infrastructure.Messaging.Interfaces;

namespace AsistOff.MES.Shared.Infrastructure.Messaging;

internal sealed class MessageBroker : IMessageBroker
{
    private readonly IMessageDispatcher _messageDispatcher;
    private readonly MessagingOptions _messagingOptions;

    public MessageBroker(IMessageDispatcher messageDispatcher, MessagingOptions messagingOptions)
    {
        _messageDispatcher = messageDispatcher;
        _messagingOptions = messagingOptions;
    }

    public async Task PublishAsync(params IMessage[]? messages)
    {
        if (messages is null)
        {
            return;
        }

        messages = messages.Where(_ => true).ToArray();

        if (messages.Length == 0)
        {
            return;
        }

        var tasks = new List<Task>();

        foreach (var message in messages)
        {
            if (!_messagingOptions.UseBackgroundDispatcher)
            {
                continue;
            }

            await _messageDispatcher.PublishAsync(message);
        }

        await Task.WhenAll(tasks);
    }
}