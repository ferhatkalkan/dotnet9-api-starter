using System.ComponentModel.DataAnnotations;

namespace OutboxRabbitMq.Api;

public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(120)]
    public required string CustomerName { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class OutboxMessage
{
    public long Id { get; set; }
    public Guid MessageId { get; set; }
    [MaxLength(256)]
    public required string MessageType { get; set; }
    public required string Payload { get; set; }
    public DateTime OccurredUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedUtc { get; set; }
    [MaxLength(64)]
    public string? CorrelationId { get; set; }
}

public sealed class ProcessedMessage
{
    public long Id { get; set; }
    public Guid MessageId { get; set; }
    [MaxLength(128)]
    public required string ConsumerName { get; set; }
    public DateTime ProcessedUtc { get; set; } = DateTime.UtcNow;
}

public sealed record OrderSubmitted(Guid MessageId, Guid OrderId, string CustomerName, decimal Amount, DateTime OccurredUtc, string? CorrelationId);
