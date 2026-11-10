using Catalog.Api.Data;
using Catalog.Api.Domain;
using Catalog.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public sealed class CatalogService : ICatalogService
{
    private const int MaxConcurrencyRetries = 3;
    private readonly CatalogDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CatalogService> _logger;

    public CatalogService(CatalogDbContext dbContext, TimeProvider timeProvider, ILogger<CatalogService> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ProductResponse> CreateProductAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var product = Product.Create(request.Name, request.Description, request.Price, request.InitialStock);

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Product {ProductId} created with initial stock {Stock}", product.Id, product.StockQuantity);

        return MapToResponse(product);
    }

    public async Task<ProductResponse?> GetProductByIdAsync(Guid productId, CancellationToken ct = default)
    {
        return await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => new ProductResponse(
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.StockQuantity,
                p.ReservedQuantity,
                p.StockQuantity - p.ReservedQuantity,
                p.CreatedAt))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ProductResponse>> GetProductsAsync(int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var limit = Math.Clamp(take, 1, 100);
        var offset = Math.Max(0, skip);

        return await _dbContext.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Skip(offset)
            .Take(limit)
            .Select(p => new ProductResponse(
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.StockQuantity,
                p.ReservedQuantity,
                p.StockQuantity - p.ReservedQuantity,
                p.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ProductResponse> AddStockAsync(Guid productId, AddStockRequest request, CancellationToken ct = default)
    {
        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
            ?? throw new ProductNotFoundException(productId);

        product.AddStock(request.Quantity);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Added {Quantity} items to product {ProductId}. New stock: {Stock}",
            request.Quantity, product.Id, product.StockQuantity);

        return MapToResponse(product);
    }

    public async Task<StockReservationResponse> ReserveStockAsync(ReserveStockRequest request, CancellationToken ct = default)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new ArgumentException("Reservation items cannot be empty.", nameof(request.Items));
        }

        var consolidatedItems = request.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new ReserveStockItem(g.Key, g.Sum(x => x.Quantity)))
            .ToList();

        var existingReservations = await _dbContext.Reservations
            .AsNoTracking()
            .Where(r => r.OrderId == request.OrderId)
            .ToListAsync(ct);

        if (existingReservations.Count > 0)
        {
            return new StockReservationResponse(
                request.OrderId,
                existingReservations.Select(r => new ReservationItemResponse(r.ProductId, r.Quantity)).ToList(),
                existingReservations[0].Status);
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
            {
                try
                {
                    await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

                    var productIds = consolidatedItems.Select(i => i.ProductId).Distinct().ToList();
                    var products = await _dbContext.Products
                        .Where(p => productIds.Contains(p.Id))
                        .ToDictionaryAsync(p => p.Id, ct);

                    var createdReservations = new List<StockReservation>();

                    foreach (var item in consolidatedItems)
                    {
                        if (!products.TryGetValue(item.ProductId, out var product))
                        {
                            throw new ProductNotFoundException(item.ProductId);
                        }

                        var reservation = product.ReserveStock(request.OrderId, item.Quantity);
                        _dbContext.Reservations.Add(reservation);
                        createdReservations.Add(reservation);
                    }

                    await _dbContext.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);

                    _logger.LogInformation("Reserved stock for order {OrderId} across {Count} items",
                        request.OrderId, createdReservations.Count);

                    return new StockReservationResponse(
                        request.OrderId,
                        createdReservations.Select(r => new ReservationItemResponse(r.ProductId, r.Quantity)).ToList(),
                        ReservationStatus.Reserved);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex, "Concurrency conflict during stock reservation for order {OrderId}, attempt {Attempt} of {MaxRetries}",
                        request.OrderId, attempt, MaxConcurrencyRetries);

                    _dbContext.ChangeTracker.Clear();

                    var productIds = consolidatedItems.Select(i => i.ProductId).Distinct().ToList();
                    var freshProducts = await _dbContext.Products
                        .AsNoTracking()
                        .Where(p => productIds.Contains(p.Id))
                        .ToDictionaryAsync(p => p.Id, ct);

                    foreach (var item in consolidatedItems)
                    {
                        if (freshProducts.TryGetValue(item.ProductId, out var product) && product.AvailableQuantity < item.Quantity)
                        {
                            throw new InsufficientStockException(item.ProductId, item.Quantity, product.AvailableQuantity);
                        }
                    }

                    if (attempt == MaxConcurrencyRetries)
                    {
                        throw;
                    }
                }
                catch (DbUpdateException)
                {
                    var concurrentReservations = await _dbContext.Reservations
                        .AsNoTracking()
                        .Where(r => r.OrderId == request.OrderId)
                        .ToListAsync(ct);

                    if (concurrentReservations.Count > 0)
                    {
                        return new StockReservationResponse(
                            request.OrderId,
                            concurrentReservations.Select(r => new ReservationItemResponse(r.ProductId, r.Quantity)).ToList(),
                            concurrentReservations[0].Status);
                    }

                    throw;
                }
            }

            throw new InvalidOperationException("Unreachable code after stock reservation retry loop.");
        });
    }

    public async Task<StockReservationResponse> ConfirmStockAsync(ConfirmStockRequest request, CancellationToken ct = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            var reservations = await _dbContext.Reservations
                .Where(r => r.OrderId == request.OrderId && r.Status == ReservationStatus.Reserved)
                .ToListAsync(ct);

            if (reservations.Count == 0)
            {
                throw new InvalidOperationException($"No active reservations found for order {request.OrderId}.");
            }

            var productIds = reservations.Select(r => r.ProductId).Distinct().ToList();
            var products = await _dbContext.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            foreach (var reservation in reservations)
            {
                if (!products.TryGetValue(reservation.ProductId, out var product))
                {
                    throw new ProductNotFoundException(reservation.ProductId);
                }

                product.ConfirmReservation(reservation.Quantity);
                reservation.Confirm();
            }

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Confirmed stock reservation for order {OrderId}", request.OrderId);

            return new StockReservationResponse(
                request.OrderId,
                reservations.Select(r => new ReservationItemResponse(r.ProductId, r.Quantity)).ToList(),
                ReservationStatus.Confirmed);
        });
    }

    public async Task<StockReservationResponse> ReleaseStockAsync(ReleaseStockRequest request, CancellationToken ct = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            var reservations = await _dbContext.Reservations
                .Where(r => r.OrderId == request.OrderId && r.Status == ReservationStatus.Reserved)
                .ToListAsync(ct);

            if (reservations.Count == 0)
            {
                throw new InvalidOperationException($"No active reservations found for order {request.OrderId}.");
            }

            var productIds = reservations.Select(r => r.ProductId).Distinct().ToList();
            var products = await _dbContext.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            foreach (var reservation in reservations)
            {
                if (!products.TryGetValue(reservation.ProductId, out var product))
                {
                    throw new ProductNotFoundException(reservation.ProductId);
                }

                product.ReleaseReservation(reservation.Quantity);
                reservation.Release();
            }

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Released stock reservation for order {OrderId}. Reason: {Reason}",
                request.OrderId, request.Reason);

            return new StockReservationResponse(
                request.OrderId,
                reservations.Select(r => new ReservationItemResponse(r.ProductId, r.Quantity)).ToList(),
                ReservationStatus.Released);
        });
    }

    private static ProductResponse MapToResponse(Product p) =>
        new(p.Id, p.Name, p.Description, p.Price, p.StockQuantity, p.ReservedQuantity, p.AvailableQuantity, p.CreatedAt);
}
