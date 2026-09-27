using System.Text.Json;
using AsistOff.MES.Multitenancy.Contracts;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Multitenancy.Entity;
using AsistOff.MES.Multitenancy.Repositories;
using AsistOff.MES.Multitenancy.Requests.Queries;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Abstractions.Jbl;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Multitenancy;

/// <summary>
/// Proves the anonymous tenant lookup minimization (issue #324):
/// <c>GET /api/tenants/{id}</c> returns the same minimal public projection as
/// registration (<c>AnonymousTenantResponse</c>: id, name, isActive) so an
/// unauthenticated caller can never harvest contact e-mail, display name or
/// settings by guessing a tenant id.
/// </summary>
public class GetTenantQueryHandlerTests
{
    private readonly Mock<ITenantRepository> _repository = new();

    private GetTenantQueryHandler CreateSut() => new(_repository.Object);

    [Fact]
    public async Task Handle_ExistingTenant_ReturnsMinimalProjection()
    {
        // Arrange — the stored row carries sensitive fields that must not leak.
        var tenantId = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant
            {
                Id = tenantId,
                Name = "acme",
                DisplayName = "Acme Corp",
                ContactEmail = "admin@acme.local",
                Settings = new TenantSettings { Country = "PL" },
                IsActive = true,
            });

        // Act
        var result = await CreateSut().Handle(new GetTenantQuery(tenantId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(tenantId);
        result.Name.Should().Be("acme");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UnknownTenant_ThrowsNotFoundException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        // Act
        var act = async () => await CreateSut().Handle(new GetTenantQuery(tenantId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public void GetTenantQuery_IsAnonymousRequestReturningMinimalProjection()
    {
        // Arrange & Act — the query stays pre-auth (no token exists for a
        // provisioning/health lookup) but its result type is the minimal
        // public projection shared with registration.
        typeof(IAllowAnonymousRequest).IsAssignableFrom(typeof(GetTenantQuery))
            .Should().BeTrue("the lookup is a pre-authentication provisioning/health lookup");

        typeof(IJblRequest<AnonymousTenantResponse>).IsAssignableFrom(typeof(GetTenantQuery))
            .Should().BeTrue("the lookup must resolve to the minimal public projection");
    }

    [Fact]
    public void AnonymousLookupResponseType_ExposesNoSensitiveMembers()
    {
        // Arrange + Act — the response type behind GET /api/tenants/{id}.
        var responseType = typeof(GetTenantQuery).GetInterfaces()
            .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IJblRequest<>))
            .GetGenericArguments()[0];
        var unwrapped = Nullable.GetUnderlyingType(responseType) ?? responseType;
        var properties = unwrapped.GetProperties().Select(p => p.Name).ToArray();

        // Assert — TenantResponse members (ContactEmail, DisplayName,
        // Settings) must never reappear on the anonymous lookup contract.
        properties.Should().BeEquivalentTo("Id", "Name", "IsActive");
    }

    [Fact]
    public async Task Handle_SerializedPayload_ContainsNoSensitiveKeys()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tenant
            {
                Id = tenantId,
                Name = "acme",
                DisplayName = "Acme Corp",
                ContactEmail = "admin@acme.local",
                Settings = new TenantSettings { Country = "PL" },
                IsActive = true,
            });

        // Act
        var result = await CreateSut().Handle(new GetTenantQuery(tenantId), CancellationToken.None);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert — exactly the minimal keys, nothing harvestable.
        using var document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo("id", "name", "isActive");

        var forbidden = new[]
        {
            "secret", "token", "password", "hash",
            "contactemail", "displayname", "settings", "country",
        };
        var lowered = json.ToLowerInvariant();
        foreach (var fragment in forbidden)
        {
            lowered.Should().NotContain(fragment,
                $"the anonymous lookup payload must not expose '{fragment}'");
        }
    }
}
