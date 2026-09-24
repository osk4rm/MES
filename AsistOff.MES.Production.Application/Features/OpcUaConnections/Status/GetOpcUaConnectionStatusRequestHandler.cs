using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.OpcUaConnections.Status;

internal sealed class GetOpcUaConnectionStatusRequestHandler(
    IOpcUaConnectionsRepository connectionsRepository,
    IMachineTelemetryTagsRepository tagsRepository,
    ITelemetryReadingsRepository readingsRepository,
    IMachinesRepository machinesRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetOpcUaConnectionStatusRequest, OpcUaConnectionStatusResponse>
{
    public async Task<OpcUaConnectionStatusResponse> Handle(GetOpcUaConnectionStatusRequest request, CancellationToken cancellationToken)
    {
        if (request.MachineId.HasValue)
        {
            // The global tenant query filter scopes the machine lookup to the
            // caller tenant, so unknown and cross-tenant ids both yield 404.
            _ = await machinesRepository.GetByIdAsync(request.MachineId.Value, cancellationToken)
                ?? throw new NotFoundException("Machine", request.MachineId.Value);
        }

        // All repositories are tenant-filtered by the global query filter, so
        // cross-tenant connections, tags and readings never affect results.
        var connections = await connectionsRepository.ListAllAsync(cancellationToken);
        var tags = await tagsRepository.ListAllAsync(cancellationToken);

        var tagsByMachine = tags
            .GroupBy(t => t.MachineId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var now = dateTimeProvider.UtcNow;

        var entries = new List<OpcUaConnectionStatusEntry>(connections.Count);
        foreach (var connection in connections
                     .Where(c => !request.MachineId.HasValue || c.MachineId == request.MachineId.Value)
                     .OrderBy(c => c.MachineId)
                     .ThenBy(c => c.EndpointUrl, StringComparer.Ordinal))
        {
            var machineTags = tagsByMachine.TryGetValue(connection.MachineId, out var list)
                ? list
                : [];
            var reporting = 0;
            foreach (var tag in machineTags)
            {
                var latest = await readingsRepository.GetLatestAsync(tag.Id, cancellationToken);
                if (OpcUaConnectionHealth.IsTagReporting(tag, latest?.ReadAt, now))
                    reporting++;
            }

            entries.Add(new OpcUaConnectionStatusEntry(
                connection.Id,
                connection.MachineId,
                connection.EndpointUrl,
                connection.IsEnabled,
                connection.LastSeenAtUtc,
                connection.LastError,
                OpcUaConnectionHealth.IsLive(connection, now),
                machineTags.Count,
                reporting,
                machineTags.Count - reporting));
        }

        return new OpcUaConnectionStatusResponse(
            entries,
            entries.Count,
            entries.Count(e => e.IsLive),
            entries.Count(e => e.IsEnabled && !e.IsLive),
            entries.Count(e => !e.IsEnabled));
    }
}
