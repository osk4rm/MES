using AsistOff.MES.Configuration.Application.Features.Operators.Create;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Configuration;

public class CreateOperatorRequestHandlerTests
{
    private readonly Mock<IOperatorsRepository> _repository = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<ITenantContext> _tenant = new();

    public CreateOperatorRequestHandlerTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(() => Guid.NewGuid());
        _tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Operator>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Operator o, CancellationToken _) => o);
    }

    private CreateOperatorRequestHandler CreateSut() =>
        new(_repository.Object, _guids.Object, _tenant.Object, NullLogger<CreateOperatorRequestHandler>.Instance);

    [Fact]
    public async Task Handle_NullDepartmentId_PersistsNullDepartmentId()
    {
        // Arrange
        Operator? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Operator>(), It.IsAny<CancellationToken>()))
            .Callback<Operator, CancellationToken>((o, _) => persisted = o)
            .ReturnsAsync((Operator o, CancellationToken _) => o);

        var request = new CreateOperatorRequest("OP-1", "Jan", "Kowalski", 50m, null, Guid.Empty);

        // Act
        var result = await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.DepartmentId.Should().BeNull();
        result.Identifier.Should().Be("OP-1");
    }

    [Fact]
    public async Task Handle_WithDepartmentId_PersistsDepartmentId()
    {
        // Arrange
        var departmentId = Guid.NewGuid();
        Operator? persisted = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Operator>(), It.IsAny<CancellationToken>()))
            .Callback<Operator, CancellationToken>((o, _) => persisted = o)
            .ReturnsAsync((Operator o, CancellationToken _) => o);

        var request = new CreateOperatorRequest("OP-2", "Anna", "Nowak", 60m, departmentId, Guid.Empty);

        // Act
        await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        persisted!.DepartmentId.Should().Be(departmentId);
    }

    [Theory]
    [InlineData("", "Jan", "Kowalski")]
    [InlineData("OP-1", "", "Kowalski")]
    [InlineData("OP-1", "Jan", "")]
    public async Task Handle_MissingRequiredFields_ThrowsValidationException(string identifier, string firstName, string lastName)
    {
        // Arrange
        var request = new CreateOperatorRequest(identifier, firstName, lastName, 10m, null, Guid.Empty);

        // Act
        var act = () => CreateSut().Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
