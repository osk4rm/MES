using AsistOff.MES.Production.Application.Features.LotGenealogy.Delete;
using AsistOff.MES.Production.Domain.Entities;
using AsistOff.MES.Production.Domain.Repositories;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class DeleteLotGenealogyEdgeRequestHandlerTests
{
    private readonly Mock<ILotGenealogyEdgesRepository> _edges = new();

    private DeleteLotGenealogyEdgeRequestHandler CreateSut() =>
        new(_edges.Object);

    private static LotGenealogyEdge NewEdge() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        ConsumedLotId = Guid.NewGuid(),
        ProducedLotId = Guid.NewGuid(),
        ProductionOrderId = Guid.NewGuid(),
        MachineId = Guid.NewGuid(),
        ConsumedQuantity = 5m,
        OccurredAt = new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
        CreatedAt = new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task Handle_KnownEdge_DeletesAsCorrection()
    {
        var edge = NewEdge();
        _edges.Setup(r => r.GetAsync(edge.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(edge);

        await CreateSut().Handle(new DeleteLotGenealogyEdgeRequest(edge.Id), CancellationToken.None);

        _edges.Verify(r => r.DeleteAsync(edge.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownEdge_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _edges.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LotGenealogyEdge?)null);

        var act = () => CreateSut().Handle(new DeleteLotGenealogyEdgeRequest(id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _edges.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
