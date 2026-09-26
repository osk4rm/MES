using System.Text.Json;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Infrastructure.Correlation;
using AsistOff.MES.Shared.Infrastructure.Errors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace AsistOff.MES.Shared.Tests.Correlation;

/// <summary>
/// Verifies the global exception handler stamps every error envelope with
/// <c>traceId</c> equal to the effective correlation ID (issue #251).
/// </summary>
public class GlobalExceptionHandlerTraceIdTests
{
    [Theory]
    [InlineData(typeof(NotFoundException), 404)]
    [InlineData(typeof(AuthenticationException), 401)]
    [InlineData(typeof(ConflictException), 409)]
    [InlineData(typeof(ConcurrencyConflictException), 409)]
    [InlineData(typeof(ValidationException), 400)]
    [InlineData(typeof(InvalidOperationException), 500)]
    public async Task TryHandle_SetsTraceId_EqualToCorrelationId(Type exceptionType, int expectedStatus)
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var context = new DefaultHttpContext();
        context.TraceIdentifier = correlationId;
        context.Items[CorrelationIds.ItemKey] = correlationId;
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var exception = (Exception)Activator.CreateInstance(exceptionType, "boom")!;

        // Act
        var handled = await handler.TryHandleAsync(context, exception, default);

        // Assert
        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(expectedStatus);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var payload = await JsonDocument.ParseAsync(context.Response.Body);
        payload.RootElement.TryGetProperty("traceId", out var traceId).Should().BeTrue();
        traceId.GetString().Should().Be(correlationId);
    }

    [Fact]
    public async Task TryHandle_ConcurrencyConflict_ExposesCurrentToken()
    {
        // Arrange — issue #263: a stale write must tell the caller the token to retry with.
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        // Act
        var handled = await handler.TryHandleAsync(
            context, new ConcurrencyConflictException("42"), default);

        // Assert
        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(409);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var payload = await JsonDocument.ParseAsync(context.Response.Body);
        payload.RootElement.TryGetProperty("concurrencyToken", out var token).Should().BeTrue();
        token.GetString().Should().Be("42");
    }
}
