using Microsoft.EntityFrameworkCore;

namespace OutboxRabbitMq.Api;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>().HasIndex(x => x.PublishedUtc);
        modelBuilder.Entity<OutboxMessage>().HasIndex(x => x.MessageId).IsUnique();
        modelBuilder.Entity<ProcessedMessage>().HasIndex(x => new { x.ConsumerName, x.MessageId }).IsUnique();
    }
}
