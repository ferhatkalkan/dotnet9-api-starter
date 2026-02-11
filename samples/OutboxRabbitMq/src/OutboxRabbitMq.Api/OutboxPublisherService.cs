using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace OutboxRabbitMq.Api;

public sealed class OutboxPublisherService(IServiceScopeFactory scopeFactory, ILogger<OutboxPublisherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

                var batch = await dbContext.OutboxMessages
                    .Where(x => x.PublishedUtc == null)
                    .OrderBy(x => x.OccurredUtc)
                    .Take(20)
                    .ToListAsync(stoppingToken);

                foreach (var message in batch)
                {
                    if (message.MessageType == nameof(OrderSubmitted))
                    {
                        var payload = JsonSerializer.Deserialize<OrderSubmitted>(message.Payload)!;
                        await publishEndpoint.Publish(payload, context =>
                        {
                            context.MessageId = payload.MessageId;
                            if (!string.IsNullOrWhiteSpace(payload.CorrelationId))
                            {
                                context.Headers.Set(CorrelationContext.HeaderName, payload.CorrelationId);
                            }
                        }, stoppingToken);
                    }

                    message.PublishedUtc = DateTime.UtcNow;
                }

                if (batch.Count > 0)
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox dispatch failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
