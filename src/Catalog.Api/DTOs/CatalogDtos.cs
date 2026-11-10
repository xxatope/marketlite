using Catalog.Api.Domain;

namespace Catalog.Api.DTOs;

public sealed record CreateProductRequest(
    string Name,
    string Description,
    decimal Price,
    int InitialStock);

public sealed record AddStockRequest(int Quantity);

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    int ReservedQuantity,
    int AvailableQuantity,
    DateTime CreatedAt);

public sealed record ReserveStockItem(Guid ProductId, int Quantity);

public sealed record ReserveStockRequest(Guid OrderId, List<ReserveStockItem> Items);

public sealed record ReservationItemResponse(Guid ProductId, int Quantity);

public sealed record StockReservationResponse(
    Guid OrderId,
    List<ReservationItemResponse> Items,
    ReservationStatus Status);

public sealed record ConfirmStockRequest(Guid OrderId);

public sealed record ReleaseStockRequest(Guid OrderId, string Reason);
