using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Order.Api.Data;
using Order.Api.Domain;
using Order.Api.DTOs;
using Order.Api.Services;
using Shared.Contracts.Events;
using Shared.Contracts.Messaging;

namespace Order.Tests;

public sealed class FakeKafkaProducer : IKafkaProducer
{
    public List<(string Topic, string Key, object Message)> DispatchedMessages { get; } = new();

    public Task ProduceAsync<T>(string topic, string key, T message, CancellationToken ct = default)
    {
        DispatchedMessages.Add((topic, key, message!));
        return Task.CompletedTask;
    }
}

public sealed class OrderServiceTests
{
    private static OrderDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new OrderDbContext(options);
    }

    [Fact]
    public async Task CreateOrderAsync_PersistsOrder_AndPublishesOrderCreatedEventToKafka()
    {
        await using var db = CreateDbContext();
        var fakeKafka = new FakeKafkaProducer();
        var service = new OrderService(db, TimeProvider.System, fakeKafka, NullLogger<OrderService>.Instance);

        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            "RUB",
            new List<CreateOrderItemRequest>
            {
                new(Guid.NewGuid(), "Gaming Mouse", 2, 75m),
                new(Guid.NewGuid(), "Mousepad", 1, 25m)
            });

        var response = await service.CreateOrderAsync(request);

        Assert.Equal(175m, response.TotalAmount);
        Assert.Equal(OrderStatus.Pending, response.Status);

        var persisted = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == response.Id);
        Assert.NotNull(persisted);
        Assert.Equal(175m, persisted.TotalAmount);
        Assert.Equal(2, persisted.Items.Count);

        Assert.Single(fakeKafka.DispatchedMessages);
        var dispatched = fakeKafka.DispatchedMessages[0];
        Assert.Equal(KafkaTopics.OrdersCreated, dispatched.Topic);
        Assert.Equal(response.Id.ToString(), dispatched.Key);
        var createdEvent = Assert.IsType<OrderCreatedEvent>(dispatched.Message);
        Assert.Equal(response.Id, createdEvent.OrderId);
        Assert.Equal(175m, createdEvent.TotalAmount);
    }

    [Fact]
    public async Task ConfirmOrderAsync_UpdatesOrder_AndPublishesOrderConfirmedEventToKafka()
    {
        await using var db = CreateDbContext();
        var fakeKafka = new FakeKafkaProducer();
        var service = new OrderService(db, TimeProvider.System, fakeKafka, NullLogger<OrderService>.Instance);

        var order = await service.CreateOrderAsync(new CreateOrderRequest(
            Guid.NewGuid(),
            "RUB",
            new List<CreateOrderItemRequest> { new(Guid.NewGuid(), "Item", 1, 100m) }));

        fakeKafka.DispatchedMessages.Clear();

        var confirmed = await service.ConfirmOrderAsync(order.Id);

        Assert.Equal(OrderStatus.Confirmed, confirmed.Status);
        Assert.NotNull(confirmed.ConfirmedAt);

        Assert.Single(fakeKafka.DispatchedMessages);
        var dispatched = fakeKafka.DispatchedMessages[0];
        Assert.Equal(KafkaTopics.OrdersConfirmed, dispatched.Topic);
        var confirmedEvent = Assert.IsType<OrderConfirmedEvent>(dispatched.Message);
        Assert.Equal(order.Id, confirmedEvent.OrderId);
    }

    [Fact]
    public async Task CancelOrderAsync_UpdatesOrder_AndPublishesOrderCancelledEventToKafka()
    {
        await using var db = CreateDbContext();
        var fakeKafka = new FakeKafkaProducer();
        var service = new OrderService(db, TimeProvider.System, fakeKafka, NullLogger<OrderService>.Instance);

        var order = await service.CreateOrderAsync(new CreateOrderRequest(
            Guid.NewGuid(),
            "RUB",
            new List<CreateOrderItemRequest> { new(Guid.NewGuid(), "Item", 1, 100m) }));

        fakeKafka.DispatchedMessages.Clear();

        var cancelled = await service.CancelOrderAsync(order.Id, "Payment declined");

        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal("Payment declined", cancelled.CancellationReason);

        Assert.Single(fakeKafka.DispatchedMessages);
        var dispatched = fakeKafka.DispatchedMessages[0];
        Assert.Equal(KafkaTopics.OrdersCancelled, dispatched.Topic);
        var cancelledEvent = Assert.IsType<OrderCancelledEvent>(dispatched.Message);
        Assert.Equal(order.Id, cancelledEvent.OrderId);
        Assert.Equal("Payment declined", cancelledEvent.Reason);
    }
}
