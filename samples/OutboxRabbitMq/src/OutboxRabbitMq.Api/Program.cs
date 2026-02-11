using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace OutboxRabbitMq.Api;

public sealed record CreateOrderRequest(string CustomerName, decimal Amount);

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();
        builder.Host.UseSerilog();

        builder.Services.AddSingleton<CorrelationContext>();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

        builder.Services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres") ?? "Host=localhost;Port=5432;Database=outbox;Username=postgres;Password=postgres"));

        builder.Services.AddMassTransit(cfg =>
        {
            cfg.SetKebabCaseEndpointNameFormatter();
            cfg.AddConsumer<OrderSubmittedConsumer>();
            cfg.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(builder.Configuration.GetConnectionString("RabbitMq") ?? "rabbitmq://localhost");
                rabbit.ConfigureEndpoints(context);
            });
        });

        builder.Services.AddHostedService<OutboxPublisherService>();
        builder.Services.AddHealthChecks().AddNpgSql(builder.Configuration.GetConnectionString("Postgres") ?? string.Empty);

        builder.Services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                RateLimitPartition.GetFixedWindowLimiter("api", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 200,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 50
                }));
        });

        var requireAuth = !builder.Configuration.GetValue("Auth:Disable", false);
        var jwtKey = builder.Configuration["Jwt:Key"] ?? "development-only-jwt-signing-key-please-change";
        var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "OutboxRabbitMq.Api";

        if (requireAuth)
        {
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidIssuer = jwtIssuer,
                        ValidAudience = jwtIssuer,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                    };
                });

            builder.Services.AddAuthorization();
        }

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "OutboxRabbitMq", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("outbox-rabbitmq-api", serviceVersion: Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0"))
            .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddConsoleExporter())
            .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation().AddConsoleExporter());

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        app.UseSerilogRequestLogging();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        app.UseRateLimiter();

        if (requireAuth)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapHealthChecks("/health");

        var ordersEndpoint = app.MapPost("/api/orders", async (CreateOrderRequest request, CorrelationContext correlationContext, AppDbContext dbContext, CancellationToken ct) =>
        {
            var order = new Order { CustomerName = request.CustomerName, Amount = request.Amount };

            var integrationEvent = new OrderSubmitted(
                MessageId: Guid.NewGuid(),
                OrderId: order.Id,
                CustomerName: order.CustomerName,
                Amount: order.Amount,
                OccurredUtc: DateTime.UtcNow,
                CorrelationId: correlationContext.CorrelationId);

            await using var tx = await dbContext.Database.BeginTransactionAsync(ct);
            await dbContext.Orders.AddAsync(order, ct);
            await dbContext.OutboxMessages.AddAsync(new OutboxMessage
            {
                MessageId = integrationEvent.MessageId,
                MessageType = nameof(OrderSubmitted),
                CorrelationId = correlationContext.CorrelationId,
                Payload = JsonSerializer.Serialize(integrationEvent)
            }, ct);
            await dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Results.Accepted($"/api/orders/{order.Id}", new { order.Id, integrationEvent.MessageId });
        });
        if (requireAuth) ordersEndpoint.RequireAuthorization();

        var pendingEndpoint = app.MapGet("/api/outbox/pending", async (AppDbContext dbContext, CancellationToken ct) =>
            Results.Ok(await dbContext.OutboxMessages.CountAsync(x => x.PublishedUtc == null, ct)));
        if (requireAuth) pendingEndpoint.RequireAuthorization();

        var processedEndpoint = app.MapGet("/api/messages/processed", async (AppDbContext dbContext, CancellationToken ct) =>
            Results.Ok(await dbContext.ProcessedMessages.CountAsync(ct)));
        if (requireAuth) processedEndpoint.RequireAuthorization();

        await app.RunAsync();
    }
}
