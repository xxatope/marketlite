namespace Shared.Contracts.Events;

public record ReservedItemDto(
    Guid ProductId,
    int Quantity);

public record StockReservedEvent(
    Guid OrderId,
    List<ReservedItemDto> Items,
    DateTime ReservedAt);

public record StockReservationFailedEvent(
    Guid OrderId,
    string Reason,
    DateTime FailedAt);
