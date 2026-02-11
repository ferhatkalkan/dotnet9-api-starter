using Serilog.Context;

namespace Template.Api;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, CorrelationContext correlationContext)
    {
        var correlationId = context.Request.Headers[CorrelationContext.HeaderName].FirstOrDefault();
        correlationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId;

        correlationContext.CorrelationId = correlationId;
        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationContext.HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
