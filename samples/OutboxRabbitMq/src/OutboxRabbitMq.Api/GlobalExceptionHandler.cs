using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace OutboxRabbitMq.Api;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, CorrelationContext correlationContext) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled error");

        var problem = new ProblemDetails
        {
            Title = "Unhandled server error",
            Type = "https://httpstatuses.com/500",
            Status = StatusCodes.Status500InternalServerError
        };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        problem.Extensions["correlationId"] = correlationContext.CorrelationId;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
