using Catalog.Api.Data;
using Catalog.Api.Domain;
using Catalog.Api.DTOs;
using Catalog.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Catalog.Tests;

public sealed class CatalogServiceTests
{
    private static CatalogDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new CatalogDbContext(options);
    }

    [Fact]
    public async Task CreateProductAsync_PersistsProduct()
    {
        await using var db = CreateDbContext();
        var service = new CatalogService(db, NullLogger<CatalogService>.Instance);

        var response = await service.CreateProductAsync(new CreateProductRequest("GPU RTX 5080", "Next-gen GPU", 1199.99m, 5));

        Assert.NotNull(response);
        Assert.Equal("GPU RTX 5080", response.Name);
        Assert.Equal(1199.99m, response.Price);
        Assert.Equal(5, response.StockQuantity);
        Assert.Equal(5, response.AvailableQuantity);

        var persisted = await db.Products.FindAsync(response.Id);
        Assert.NotNull(persisted);
        Assert.Equal("GPU RTX 5080", persisted.Name);
    }

    [Fact]
    public async Task AddStockAsync_UpdatesStockInDatabase()
    {
        await using var db = CreateDbContext();
        var service = new CatalogService(db, NullLogger<CatalogService>.Instance);
        var product = await service.CreateProductAsync(new CreateProductRequest("RAM DDR5", "32GB", 120m, 10));

        var updated = await service.AddStockAsync(product.Id, new AddStockRequest(15));

        Assert.Equal(25, updated.StockQuantity);
        Assert.Equal(25, updated.AvailableQuantity);
    }

    [Fact]
    public async Task GetProductsAsync_ProvidesStablePaginationAcrossIdenticalNames()
    {
        await using var db = CreateDbContext();
        var service = new CatalogService(db, NullLogger<CatalogService>.Instance);

        for (var i = 0; i < 5; i++)
        {
            await service.CreateProductAsync(new CreateProductRequest("Identical Name", $"Desc {i}", 10m, 5));
        }

        var page1 = await service.GetProductsAsync(skip: 0, take: 2);
        var page2 = await service.GetProductsAsync(skip: 2, take: 2);

        Assert.Equal(2, page1.Count);
        Assert.Equal(2, page2.Count);
        Assert.DoesNotContain(page1, p1 => page2.Any(p2 => p2.Id == p1.Id));
    }

    [Fact]
    public async Task ReserveStockAsync_CreatesReservationsAndUpdatesAvailable()
    {
        await using var db = CreateDbContext();
        var service = new CatalogService(db, NullLogger<CatalogService>.Instance);
        var p1 = await service.CreateProductAsync(new CreateProductRequest("CPU", "8 cores", 300m, 10));
        var p2 = await service.CreateProductAsync(new CreateProductRequest("Motherboard", "B650", 180m, 8));

        var orderId = Guid.NewGuid();
        var reserveRequest = new ReserveStockRequest(orderId, new List<ReserveStockItem>
        {
            new(p1.Id, 2),
            new(p2.Id, 1)
        });

        var response = await service.ReserveStockAsync(reserveRequest);

        Assert.Equal(orderId, response.OrderId);
        Assert.Equal(ReservationStatus.Reserved, response.Status);
        Assert.Equal(2, response.Items.Count);

        var reloadedP1 = await service.GetProductByIdAsync(p1.Id);
        Assert.NotNull(reloadedP1);
        Assert.Equal(10, reloadedP1.StockQuantity);
        Assert.Equal(2, reloadedP1.ReservedQuantity);
        Assert.Equal(8, reloadedP1.AvailableQuantity);
    }

    [Fact]
    public async Task ConfirmStockAsync_DeductsFromPhysicalStock()
    {
        await using var db = CreateDbContext();
        var service = new CatalogService(db, NullLogger<CatalogService>.Instance);
        var p = await service.CreateProductAsync(new CreateProductRequest("SSD", "2TB NVMe", 150m, 20));

        var orderId = Guid.NewGuid();
        await service.ReserveStockAsync(new ReserveStockRequest(orderId, new List<ReserveStockItem> { new(p.Id, 5) }));

        var confirmed = await service.ConfirmStockAsync(new ConfirmStockRequest(orderId));

        Assert.Equal(ReservationStatus.Confirmed, confirmed.Status);

        var reloaded = await service.GetProductByIdAsync(p.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(15, reloaded.StockQuantity);
        Assert.Equal(0, reloaded.ReservedQuantity);
        Assert.Equal(15, reloaded.AvailableQuantity);
    }

    [Fact]
    public async Task ReleaseStockAsync_RestoresAvailableStock()
    {
        await using var db = CreateDbContext();
        var service = new CatalogService(db, NullLogger<CatalogService>.Instance);
        var p = await service.CreateProductAsync(new CreateProductRequest("Power Supply", "850W Gold", 130m, 10));

        var orderId = Guid.NewGuid();
        await service.ReserveStockAsync(new ReserveStockRequest(orderId, new List<ReserveStockItem> { new(p.Id, 4) }));

        var released = await service.ReleaseStockAsync(new ReleaseStockRequest(orderId, "Payment timed out"));

        Assert.Equal(ReservationStatus.Released, released.Status);

        var reloaded = await service.GetProductByIdAsync(p.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(10, reloaded.StockQuantity);
        Assert.Equal(0, reloaded.ReservedQuantity);
        Assert.Equal(10, reloaded.AvailableQuantity);
    }
}
