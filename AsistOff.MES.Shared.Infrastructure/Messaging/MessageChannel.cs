using System.Threading.Channels;
using AsistOff.MES.Shared.Abstractions.Messaging;
using AsistOff.MES.Shared.Infrastructure.Messaging.Interfaces;

namespace AsistOff.MES.Shared.Infrastructure.Messaging;

internal sealed class MessageChannel : IMessageChannel
{
    private readonly Channel<IMessage> _messages = Channel.CreateUnbounded<IMessage>();

    public ChannelReader<IMessage> Reader => _messages.Reader;
    public ChannelWriter<IMessage> Writer => _messages.Writer;
}