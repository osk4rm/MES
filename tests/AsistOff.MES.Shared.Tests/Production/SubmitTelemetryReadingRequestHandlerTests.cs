using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Production.Application.Features.TelemetryReadings.Submit;
using AsistOff.MES.Production.Application.Features.TelemetryReadings;
using AsistOff.MES.Production.Domain.Enums;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Production;

public class SubmitTelemetryReadingRequestHandlerTests
{
    private readonly Mock<ITelemetryIngestionService> _ingestion = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _tagId = Guid.NewGuid();
    private readonly DateTime _readAt = new(2026, 9, 24, 11, 50, 0, DateTimeKind.Utc);

    public SubmitTelemetryReadingRequestHandlerTests()
    {
        _tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
    }

    private SubmitTelemetryReadingRequestHandler CreateSut() =>
        new(_ingestion.Object, _tenant.Object);

    [Fact]
    public async Task Handle_DelegatesToIngestionService_WithCallerTenant()
    {
        // Arrange
        var expected = new TelemetryReadingResponse(
            Guid.NewGuid(), _tagId, Guid.NewGuid(), _readAt, 21.5, null, TelemetryQuality.Good);
        _ingestion.Setup(i => i.IngestAsync(
                _tagId, _readAt, 21.5, null, TelemetryQuality.Good, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var request = new SubmitTelemetryReadingRequest(_tagId, _readAt, 21.5, null, TelemetryQuality.Good);

        // Act
        var result = await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        result.Should().Be(expected);
        _ingestion.Verify(i => i.IngestAsync(
            _tagId, _readAt, 21.5, null, TelemetryQuality.Good, _tenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
