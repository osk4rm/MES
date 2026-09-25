using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Auth;
using FluentAssertions;
using Moq;

namespace AsistOff.MES.Shared.Tests.Auth;

public class AuthManagerKeyFloorTests
{
    [Fact]
    public void Ctor_ShortKey_ThrowsNamingKey()
    {
        // Arrange
        var options = new AuthOptions
        {
            IssuerSigningKey = "too-short",
            Issuer = "AsistOff.MES",
            Audience = "AsistOff.MES"
        };
        var clock = new Mock<IDateTimeProvider>();

        // Act
        var act = () => new AuthManager(options, clock.Object);

        // Assert — startup must name the offending setting.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*auth:IssuerSigningKey*256 bits*");
    }

    [Fact]
    public void Ctor_MissingKey_ThrowsNamingKey()
    {
        // Arrange
        var options = new AuthOptions
        {
            IssuerSigningKey = "",
            Issuer = "AsistOff.MES",
            Audience = "AsistOff.MES"
        };
        var clock = new Mock<IDateTimeProvider>();

        // Act
        var act = () => new AuthManager(options, clock.Object);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*auth:IssuerSigningKey*");
    }
}
