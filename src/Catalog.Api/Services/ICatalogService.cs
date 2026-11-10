using Catalog.Api.DTOs;

namespace Catalog.Api.Services;

public interface ICatalogService
{
    Task<ProductResponse> CreateProductAsync(CreateProductRequest request, CancellationToken ct = default);
    Task<ProductResponse?> GetProductByIdAsync(Guid productId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductResponse>> GetProductsAsync(int skip = 0, int take = 50, CancellationToken ct = default);
    Task<ProductResponse> AddStockAsync(Guid productId, AddStockRequest request, CancellationToken ct = default);
    Task<StockReservationResponse> ReserveStockAsync(ReserveStockRequest request, CancellationToken ct = default);
    Task<StockReservationResponse> ConfirmStockAsync(ConfirmStockRequest request, CancellationToken ct = default);
    Task<StockReservationResponse> ReleaseStockAsync(ReleaseStockRequest request, CancellationToken ct = default);
}
