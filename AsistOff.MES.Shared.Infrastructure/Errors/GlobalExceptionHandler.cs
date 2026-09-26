using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Shared.Infrastructure.Correlation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Net;

namespace AsistOff.MES.Shared.Infrastructure.Errors;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "An exception occurred: {Message}", exception.Message);

        var problemDetails = CreateProblemDetails(exception, httpContext);

        httpContext.Response.StatusCode = problemDetails.Status ?? (int)HttpStatusCode.InternalServerError;

        // Serialize with the runtime type: the static type here is
        // ProblemDetails, so serializing as ProblemDetails would drop the
        // ValidationProblemDetails.Errors dictionary and callers would never
        // see which field failed (e.g. sort whitelist rejections must name
        // the field, issue #311). The content type is passed explicitly
        // because WriteAsJsonAsync defaults to application/json and would
        // otherwise overwrite the problem+json envelope type.
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            problemDetails.GetType(),
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }

    private static ProblemDetails CreateProblemDetails(Exception exception, HttpContext httpContext)
    {
        ProblemDetails problemDetails = exception switch
        {
            ValidationException validationEx => new ValidationProblemDetails(validationEx.Errors)
            {
                Title = "One or more validation errors occurred.",
                Status = (int)HttpStatusCode.BadRequest,
                Detail = validationEx.Message
            },
            ConcurrencyConflictException concurrencyEx => new ProblemDetails
            {
                Title = "Conflict",
                Status = (int)HttpStatusCode.Conflict,
                Detail = concurrencyEx.Message,
                Extensions = { ["concurrencyToken"] = concurrencyEx.CurrentToken }
            },
            // Derived before its base: GanttScheduleConflictException extends
            // ConflictException, so this arm must precede the plain 409 arm.
            GanttScheduleConflictException ganttEx => new ProblemDetails
            {
                Title = "Conflict",
                Status = (int)HttpStatusCode.Conflict,
                Detail = ganttEx.Message,
                Extensions = { ["conflictingSegmentIds"] = ganttEx.ConflictingSegmentIds }
            },
            ConflictException conflictEx => new ProblemDetails
            {
                Title = "Conflict",
                Status = (int)HttpStatusCode.Conflict,
                Detail = conflictEx.Message
            },
            NotFoundException notFoundEx => new ProblemDetails
            {
                Title = "Not Found",
                Status = (int)HttpStatusCode.NotFound,
                Detail = notFoundEx.Message
            },
            AuthenticationException authEx => new ProblemDetails
            {
                Title = "Unauthorized",
                Status = (int)HttpStatusCode.Unauthorized,
                Detail = authEx.Message
            },
            ForbiddenException forbiddenEx => new ProblemDetails
            {
                Title = "Forbidden",
                Status = (int)HttpStatusCode.Forbidden,
                Detail = forbiddenEx.Message
            },
            UnauthorizedAccessException uaEx => new ProblemDetails
            {
                Title = "Unauthorized",
                Status = (int)HttpStatusCode.Unauthorized,
                Detail = uaEx.Message
            },
            RepositoryException repoEx => new ProblemDetails
            {
                Title = "Repository Error",
                Status = (int)HttpStatusCode.InternalServerError,
                Detail = repoEx.Message
            },
            _ => new ProblemDetails
            {
                Title = "An error occurred",
                Status = (int)HttpStatusCode.InternalServerError,
                Detail = "An unexpected error occurred."
            }
        };

        // Every error envelope carries traceId equal to the effective
        // X-Correlation-ID so support can link an operator-visible error to
        // a backend log line (issue #251).
        var traceId = CorrelationIds.GetCurrent(httpContext) ?? httpContext.TraceIdentifier;
        problemDetails.Extensions["traceId"] = traceId;

        return problemDetails;
    }
}
