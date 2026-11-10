using Microsoft.EntityFrameworkCore;
using Order.Api.Data;
using Order.Api.DTOs;
using Order.Api.Infrastructure;
using Order.Api.Services;
using Shared.Contracts.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);

var connectionString = builder.Configuration.GetConnectionString("OrderDb")
    ?? throw new InvalidOperationException("Connection string 'OrderDb' not configured.");

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(connectionString));

var kafkaBootstrap = builder.Configuration.GetValue<string>("Kafka:BootstrapServers") ?? "localhost:9092";
builder.Services.AddSingleton<IKafkaProducer>(_ => new KafkaProducer(kafkaBootstrap));

builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

var orders = app.MapGroup("/api/orders").WithTags("Orders");

orders.MapPost("/", async (CreateOrderRequest request, IOrderService service, CancellationToken ct) =>
{
    var created = await service.CreateOrderAsync(request, ct);
    return Results.Created($"/api/orders/{created.Id}", created);
});

orders.MapGet("/{id:guid}", async (Guid id, IOrderService service, CancellationToken ct) =>
{
    var order = await service.GetOrderByIdAsync(id, ct);
    return order is not null ? Results.Ok(order) : Results.NotFound();
});

orders.MapPost("/{id:guid}/confirm", async (Guid id, IOrderService service, CancellationToken ct) =>
{
    var confirmed = await service.ConfirmOrderAsync(id, ct);
    return Results.Ok(confirmed);
});

orders.MapPost("/{id:guid}/cancel", async (Guid id, CancelOrderRequest request, IOrderService service, CancellationToken ct) =>
{
    var cancelled = await service.CancelOrderAsync(id, request.Reason, ct);
    return Results.Ok(cancelled);
});

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Order.Api" }));

app.Run();
