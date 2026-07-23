using FluentValidation;
using InventoryFlow.Application.Features.Authentication;
using InventoryFlow.Application.Features.Collaboration;
using InventoryFlow.Application.Features.Inventory;
using InventoryFlow.Application.Features.Products;
using InventoryFlow.Application.Features.Suppliers;
using InventoryFlow.Application.Features.Warehouses;
using InventoryFlow.Domain.Exceptions;
using InventoryFlow.Domain.Entities;
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

        var statusCode = exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            DomainException => StatusCodes.Status400BadRequest,
            AuthenticationException => StatusCodes.Status401Unauthorized,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            ProductSkuConflictException => StatusCodes.Status409Conflict,
            SupplierNameConflictException => StatusCodes.Status409Conflict,
            WarehouseNameConflictException => StatusCodes.Status409Conflict,
            InsufficientInventoryException => StatusCodes.Status409Conflict,
            InventoryArchiveConflictException => StatusCodes.Status409Conflict,
            CollaborationConflictException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        logger.LogError(
            exception,
            "Unhandled exception while processing {RequestMethod} {RequestPath}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        if (exception is ValidationException validationException)
        {
            var validation = new ValidationProblemDetails(validationException.Errors.GroupBy(error => error.PropertyName).ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray()))
            { Status = statusCode, Title = "One or more validation errors occurred.", Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1" };
            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(validation, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
            return true;
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch { StatusCodes.Status400BadRequest => "A business rule was violated.", StatusCodes.Status401Unauthorized => "Authentication failed.", StatusCodes.Status403Forbidden => "Access is forbidden.", StatusCodes.Status409Conflict => "A conflicting value or insufficient inventory exists.", _ => "An unexpected error occurred." },
            Type = statusCode switch
            {
                StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            },
            Detail = exception is DomainException or InsufficientInventoryException or InventoryArchiveConflictException
                ? exception.Message
                : null,
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
