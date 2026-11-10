namespace Shared.Contracts.Events;

public record OrderItemDto(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);

public record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    string Currency,
    List<OrderItemDto> Items,
    DateTime CreatedAt);

public record OrderConfirmedEvent(
    Guid OrderId,
    DateTime ConfirmedAt);

public record OrderCancelledEvent(
    Guid OrderId,
    string Reason,
    DateTime CancelledAt);
