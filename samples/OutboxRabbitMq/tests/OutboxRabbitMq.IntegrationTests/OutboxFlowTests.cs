using System.Net;
using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using OutboxRabbitMq.Api;

namespace OutboxRabbitMq.IntegrationTests;

public sealed class OutboxFlowTests : IAsyncLifetime
{
    private readonly IContainer _rabbitMq = new ContainerBuilder()
        .WithImage("rabbitmq:3-management")
        .WithPortBinding(5672, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5672))
        .Build();

    private readonly IContainer _postgres = new ContainerBuilder()
        .WithImage("postgres:16")
        .WithEnvironment("POSTGRES_USER", "postgres")
        .WithEnvironment("POSTGRES_PASSWORD", "postgres")
        .WithEnvironment("POSTGRES_DB", "outbox")
        .WithPortBinding(5432, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    public async Task InitializeAsync()
    {
        await _rabbitMq.StartAsync();
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _rabbitMq.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Should_Persist_And_Process_Outbox_Message()
    {
        var rabbitPort = _rabbitMq.GetMappedPublicPort(5672);
        var pgPort = _postgres.GetMappedPublicPort(5432);

        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", $"Host=localhost;Port={pgPort};Database=outbox;Username=postgres;Password=postgres");
            builder.UseSetting("ConnectionStrings:RabbitMq", $"rabbitmq://localhost:{rabbitPort}");
            builder.UseSetting("Auth:Disable", "true");
        });

        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/orders", new { customerName = "Ada", amount = 42.5m });
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var processed = 0;
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(1000);
            await using var connection = new NpgsqlConnection($"Host=localhost;Port={pgPort};Database=outbox;Username=postgres;Password=postgres");
            await connection.OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"ProcessedMessages\"", connection);
            processed = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            if (processed > 0) break;
        }

        processed.Should().BeGreaterThan(0);
    }

}
