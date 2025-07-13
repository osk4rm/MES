using System.Threading.Channels;
using AsistOff.MES.Shared.Abstractions.Messaging;

namespace AsistOff.MES.Shared.Infrastructure.Messaging.Interfaces;

public interface IMessageChannel
{
    ChannelReader<IMessage> Reader { get; }
    ChannelWriter<IMessage> Writer { get; }
}