using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Production.Application.Features.Oee.Snapshot;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Production.Application.Features.Oee.Summary;

internal sealed class GetOeeSummaryRequestHandler(
    IMachinesRepository machinesRepository,
    IProductionConfirmationsRepository confirmationsRepository)
    : IRequestHandler<GetOeeSummaryRequest, OeeSummaryResponse>
{
    public async Task<OeeSummaryResponse> Handle(GetOeeSummaryRequest request, CancellationToken cancellationToken)
    {
        if (request.MachineId == Guid.Empty)
            throw new ValidationException(nameof(request.MachineId), "Machine is required.");

        if (request.FromUtc == default || request.ToUtc == default)
            throw new ValidationException(nameof(request.FromUtc), "Time window is required.");

        var fromUtc = request.FromUtc.ToUniversalTime();
        var toUtc = request.ToUtc.ToUniversalTime();

        if (fromUtc >= toUtc)
            throw new ValidationException(nameof(request.FromUtc), "FromUtc must be before ToUtc.");

        // The global tenant query filter scopes the machine lookup to the
        // caller tenant, so unknown and cross-tenant ids both yield 404.
        _ = await machinesRepository.GetByIdAsync(request.MachineId, cancellationToken)
            ?? throw new NotFoundException("Machine", request.MachineId);

        var confirmations = await confirmationsRepository.ListForMachineInWindowAsync(
            request.MachineId, fromUtc, toUtc, cancellationToken);

        // Quality uses confirmations only, so ScrapEvent rows never double count.
        var goodCount = confirmations.Sum(c => c.GoodQuantity);
        var scrapCount = confirmations.Sum(c => c.ScrapQuantity);
        var totalCount = goodCount + scrapCount;

        double? quality = totalCount > 0
            ? OeeMath.Round4((double)(goodCount / totalCount))
            : null;

        return new OeeSummaryResponse(
            request.MachineId,
            fromUtc,
            toUtc,
            goodCount,
            scrapCount,
            totalCount,
            quality);
    }
}
