using System.Text.Json;
using AsistOff.MES.Multitenancy.Contracts;
using AsistOff.MES.Multitenancy.Requests.Commands.Create;
using AsistOff.MES.Shared.Abstractions.Events;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Users.Core.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Multitenancy;

/// <summary>
/// Proves the anonymous tenant self-registration projection: the handler
/// returns only id, name and active status, so the HTTP response can never
/// leak secrets, tokens, connection strings or internal flags.
/// </summary>
public class AnonymousTenantResponseTests
{
    private readonly Mock<ITenantRepository> _repository = new();
    private readonly Mock<IGuidProvider> _guids = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IEventDispatcher> _events = new();
    private readonly Mock<IPasswordHasher<User>> _hasher = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    public AnonymousTenantResponseTests()
    {
        _guids.Setup(g => g.NewGuid()).Returns(_tenantId);
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc));
        _hasher.Setup(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
            .Returns("HASHED");
    }

    private CreateTenantCommandHandler CreateSut() =>
        new(_repository.Object, _guids.Object, _clock.Object,
            NullLogger<CreateTenantCommandHandler>.Instance, _events.Object, _hasher.Object);

    [Fact]
    public async Task Handle_ValidCommand_ReturnsOnlyIdNameAndIsActive()
    {
        // Arrange
        var request = new CreateTenantCommand(
            "acme", "Acme Corp", "admin@acme.local", string.Empty, "Passw0rd!", "Passw0rd!");

        // Act
        var result = await CreateSut().Handle(request, CancellationToken.None);

        // Assert
        result.Id.Should().Be(_tenantId);
        result.Name.Should().Be("acme");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public void AnonymousTenantResponse_ExposesExactlyThreeProperties()
    {
        // Arrange + Act
        var properties = typeof(AnonymousTenantResponse).GetProperties().Select(p => p.Name).ToArray();

        // Assert
        properties.Should().BeEquivalentTo("Id", "Name", "IsActive");
    }

    [Fact]
    public async Task Handle_SerializedPayload_ContainsNoSecretMaterial()
    {
        // Arrange
        var request = new CreateTenantCommand(
            "acme", "Acme Corp", "admin@acme.local", string.Empty, "Passw0rd!", "Passw0rd!");

        // Act
        var result = await CreateSut().Handle(request, CancellationToken.None);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        json.Should().Contain("\"id\"");
        json.Should().Contain("\"name\"");
        json.Should().Contain("\"isActive\"");

        var forbidden = new[]
        {
            "secret", "token", "connectionstring", "password", "hash",
            "contactemail", "displayname", "settings", "isadmin", "flag", "internal"
        };
        var lowered = json.ToLowerInvariant();
        foreach (var fragment in forbidden)
        {
            lowered.Should().NotContain(fragment,
                $"the anonymous payload must not expose '{fragment}'");
        }
    }
}
