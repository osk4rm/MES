using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using MediatR;

namespace AsistOff.MES.Configuration.Application.Features.ReasonCodes.Update;

internal sealed class UpdateReasonCodeRequestHandler(IReasonCodesRepository repository)
    : IRequestHandler<UpdateReasonCodeRequest>
{
    public async Task Handle(UpdateReasonCodeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ValidationException(nameof(request.Code), "Code is required");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException(nameof(request.Name), "Name is required");

        var reasonCode = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("ReasonCode", request.Id);

        if (await repository.CodeExistsAsync(request.Code, request.Id, cancellationToken))
            throw new ConflictException($"Reason code with code '{request.Code}' already exists.");

        reasonCode.Code = request.Code;
        reasonCode.Name = request.Name;
        reasonCode.Description = request.Description;
        reasonCode.Category = request.Category;
        reasonCode.IsActive = request.IsActive;
        reasonCode.SortIndex = request.SortIndex;

        await repository.UpdateAsync(reasonCode, cancellationToken);
    }
}
