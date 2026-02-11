using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace OutboxRabbitMq.Api;

public sealed class OrderSubmittedConsumer(IServiceScopeFactory scopeFactory, ILogger<OrderSubmittedConsumer> logger) : IConsumer<OrderSubmitted>
{
    public async Task Consume(ConsumeContext<OrderSubmitted> context)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exists = await dbContext.ProcessedMessages
            .AnyAsync(x => x.ConsumerName == nameof(OrderSubmittedConsumer) && x.MessageId == context.Message.MessageId, context.CancellationToken);

        if (exists)
        {
            logger.LogInformation("Skipping duplicate message {MessageId}", context.Message.MessageId);
            return;
        }

        dbContext.ProcessedMessages.Add(new ProcessedMessage
        {
            ConsumerName = nameof(OrderSubmittedConsumer),
            MessageId = context.Message.MessageId
        });

        await dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
