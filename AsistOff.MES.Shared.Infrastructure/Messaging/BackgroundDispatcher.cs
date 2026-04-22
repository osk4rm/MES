using AsistOff.MES.Shared.Infrastructure.Messaging.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Shared.Infrastructure.Messaging;

public class BackgroundDispatcher : BackgroundService
{
    private readonly ILogger<BackgroundDispatcher> _logger;
    private readonly IMessageChannel _messageChannel;
    private readonly IMessageDispatcher _messageDispatcher;

    public BackgroundDispatcher(ILogger<BackgroundDispatcher> logger, IMessageChannel messageChannel, IMessageDispatcher messageDispatcher)
    {
        _logger = logger;
        _messageChannel = messageChannel;
        _messageDispatcher = messageDispatcher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _messageChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await _messageDispatcher.PublishAsync(message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, exception.Message);
            }
        }
    }
}