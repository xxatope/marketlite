using Catalog.Api.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Infrastructure;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            ProductNotFoundException =>
                (StatusCodes.Status404NotFound, "Resource Not Found", exception.Message),

            InsufficientStockException or FractionalCentsException =>
                (StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", exception.Message),

            InvalidReservationStateException or ArgumentException or InvalidOperationException =>
                (StatusCodes.Status400BadRequest, "Bad Request", exception.Message),

            _ => HandleInternalError(exception)
        };

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private (int Status, string Title, string Detail) HandleInternalError(Exception exception)
    {
        _logger.LogError(exception, "An unhandled server exception occurred.");
        return (StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred. Please contact support.");
    }
}
