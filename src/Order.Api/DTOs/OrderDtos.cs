using Order.Api.Domain;

namespace Order.Api.DTOs;

public sealed record CreateOrderItemRequest(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);

public sealed record CreateOrderRequest(
    Guid CustomerId,
    string Currency,
    List<CreateOrderItemRequest> Items);

public sealed record OrderItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);

public sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    string Currency,
    decimal TotalAmount,
    OrderStatus Status,
    IReadOnlyList<OrderItemResponse> Items,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    DateTime? CancelledAt,
    string? CancellationReason);

public sealed record CancelOrderRequest(string Reason);
