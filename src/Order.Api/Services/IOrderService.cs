using Order.Api.DTOs;

namespace Order.Api.Services;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task<OrderResponse?> GetOrderByIdAsync(Guid id, CancellationToken ct = default);
    Task<OrderResponse> ConfirmOrderAsync(Guid id, CancellationToken ct = default);
    Task<OrderResponse> CancelOrderAsync(Guid id, string reason, CancellationToken ct = default);
}
