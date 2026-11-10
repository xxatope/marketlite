using Microsoft.EntityFrameworkCore;
using Order.Api.Data;
using Order.Api.Domain;
using Order.Api.DTOs;
using Shared.Contracts.Events;
using Shared.Contracts.Messaging;

namespace Order.Api.Services;

public sealed class OrderService : IOrderService
{
    private readonly OrderDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        OrderDbContext dbContext,
        TimeProvider timeProvider,
        IKafkaProducer kafkaProducer,
        ILogger<OrderService> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        var items = request.Items.Select(i => (i.ProductId, i.ProductName, i.Quantity, i.UnitPrice));
        var order = Domain.Order.Create(request.CustomerId, request.Currency, items, _timeProvider);

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Order {OrderId} created with total amount {Amount} {Currency}",
            order.Id, order.TotalAmount, order.Currency);

        var orderCreatedEvent = new OrderCreatedEvent(
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            order.Currency,
            order.Items.Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice)).ToList(),
            order.CreatedAt);

        await _kafkaProducer.ProduceAsync(KafkaTopics.OrdersCreated, order.Id.ToString(), orderCreatedEvent, ct);

        return MapToResponse(order);
    }

    public async Task<OrderResponse?> GetOrderByIdAsync(Guid id, CancellationToken ct = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        return order is null ? null : MapToResponse(order);
    }

    public async Task<OrderResponse> ConfirmOrderAsync(Guid id, CancellationToken ct = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            var order = await _dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id, ct)
                ?? throw new OrderNotFoundException(id);

            order.Confirm(_timeProvider);
            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Order {OrderId} confirmed", order.Id);

            var confirmedEvent = new OrderConfirmedEvent(order.Id, order.ConfirmedAt!.Value);
            await _kafkaProducer.ProduceAsync(KafkaTopics.OrdersConfirmed, order.Id.ToString(), confirmedEvent, ct);

            return MapToResponse(order);
        });
    }

    public async Task<OrderResponse> CancelOrderAsync(Guid id, string reason, CancellationToken ct = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            var order = await _dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id, ct)
                ?? throw new OrderNotFoundException(id);

            order.Cancel(reason, _timeProvider);
            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Order {OrderId} cancelled. Reason: {Reason}", order.Id, reason);

            var cancelledEvent = new OrderCancelledEvent(order.Id, reason, order.CancelledAt!.Value);
            await _kafkaProducer.ProduceAsync(KafkaTopics.OrdersCancelled, order.Id.ToString(), cancelledEvent, ct);

            return MapToResponse(order);
        });
    }

    private static OrderResponse MapToResponse(Domain.Order o) =>
        new(
            o.Id,
            o.CustomerId,
            o.Currency,
            o.TotalAmount,
            o.Status,
            o.Items.Select(i => new OrderItemResponse(i.Id, i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.Subtotal)).ToList(),
            o.CreatedAt,
            o.ConfirmedAt,
            o.CancelledAt,
            o.CancellationReason);
}
