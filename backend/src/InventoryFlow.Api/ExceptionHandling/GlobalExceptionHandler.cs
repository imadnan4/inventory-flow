using InventoryFlow.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InventoryFlow.Api.ExceptionHandling;

/// <summary>
/// Converts unhandled application exceptions into RFC 7807 problem details responses.
/// </summary>
/// <param name="logger">The logger used to capture unexpected failures.</param>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted)
        {
            logger.LogWarning(
                exception,
                "The response has already started for {RequestMethod} {RequestPath}",
                httpContext.Request.Method,
                httpContext.Request.Path);

            return false;
        }

        var statusCode = exception is DomainException
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status500InternalServerError;

        logger.LogError(
            exception,
            "Unhandled exception while processing {RequestMethod} {RequestPath}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status400BadRequest
                ? "A business rule was violated."
                : "An unexpected error occurred.",
            Type = statusCode == StatusCodes.Status400BadRequest
                ? "https://tools.ietf.org/html/rfc9110#section-15.5.1"
                : "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            Detail = exception is DomainException ? exception.Message : null,
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }
}
