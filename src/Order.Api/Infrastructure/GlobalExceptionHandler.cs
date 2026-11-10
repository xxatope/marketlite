using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Order.Api.Domain;

namespace Order.Api.Infrastructure;

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
            OrderNotFoundException =>
                (StatusCodes.Status404NotFound, "Resource Not Found", exception.Message),

            EmptyOrderException =>
                (StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity", exception.Message),

            DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict, "Concurrency Conflict", exception.Message),

            InvalidOrderStateException or ArgumentException =>
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
