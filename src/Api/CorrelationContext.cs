namespace Template.Api;

public sealed class CorrelationContext
{
    public const string HeaderName = "X-Correlation-Id";
    public string CorrelationId { get; set; } = string.Empty;
}
