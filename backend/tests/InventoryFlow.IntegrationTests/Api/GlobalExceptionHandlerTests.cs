using System.Text.Json;
using InventoryFlow.Api.ExceptionHandling;
using InventoryFlow.Domain.Exceptions;
using InventoryFlow.Application.Features.Products;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryFlow.IntegrationTests.Api;

/// <summary>
/// Verifies exception responses produced by <see cref="GlobalExceptionHandler"/>.
/// </summary>
public sealed class GlobalExceptionHandlerTests
{
    /// <summary>
    /// Produces an RFC 7807 response for a domain exception.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_WithDomainException_WritesProblemDetailsMediaType()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance);

        // Act
        var handled = await handler.TryHandleAsync(
            httpContext,
            new DomainException("Product SKU must be unique."),
            CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.Equal("application/problem+json", httpContext.Response.ContentType);

        httpContext.Response.Body.Position = 0;
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            httpContext.Response.Body);
        Assert.NotNull(problemDetails);
        Assert.Equal("Product SKU must be unique.", problemDetails.Detail);
    }
    /// <summary>Produces a conflict response for duplicate SKU exceptions.</summary>
    [Fact]
    public async Task TryHandleAsync_WithProductSkuConflict_WritesConflictProblemDetails()
    {
        var httpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var handled = await handler.TryHandleAsync(httpContext, new ProductSkuConflictException(), CancellationToken.None);
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, httpContext.Response.StatusCode);
    }
}
