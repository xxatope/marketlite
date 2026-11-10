using Catalog.Api.Domain;

namespace Catalog.Tests;

public sealed class ProductDomainTests
{
    [Fact]
    public void Create_InitializesProductCorrectly()
    {
        var product = Product.Create("Mechanical Keyboard", "RGB switches", 129.99m, 50);

        Assert.Equal("Mechanical Keyboard", product.Name);
        Assert.Equal("RGB switches", product.Description);
        Assert.Equal(129.99m, product.Price);
        Assert.Equal(50, product.StockQuantity);
        Assert.Equal(0, product.ReservedQuantity);
        Assert.Equal(50, product.AvailableQuantity);
    }

    [Theory]
    [InlineData("", 100, 10)]
    [InlineData("   ", 100, 10)]
    [InlineData("Valid", 0, 10)]
    [InlineData("Valid", -5, 10)]
    [InlineData("Valid", 10, -1)]
    public void Create_ThrowsWhenArgumentsAreInvalid(string name, decimal price, int stock)
    {
        Assert.ThrowsAny<ArgumentException>(() => Product.Create(name, "Desc", price, stock));
    }

    [Fact]
    public void AddStock_IncreasesStockQuantity()
    {
        var product = Product.Create("Mouse", "Wireless", 49.99m, 20);

        product.AddStock(15);

        Assert.Equal(35, product.StockQuantity);
        Assert.Equal(35, product.AvailableQuantity);
    }

    [Fact]
    public void ReserveStock_IncreasesReservedQuantity()
    {
        var product = Product.Create("Monitor", "4K 144Hz", 399.99m, 10);
        var orderId = Guid.NewGuid();

        var reservation = product.ReserveStock(orderId, 4);

        Assert.Equal(10, product.StockQuantity);
        Assert.Equal(4, product.ReservedQuantity);
        Assert.Equal(6, product.AvailableQuantity);
        Assert.Equal(orderId, reservation.OrderId);
        Assert.Equal(product.Id, reservation.ProductId);
        Assert.Equal(4, reservation.Quantity);
        Assert.Equal(ReservationStatus.Reserved, reservation.Status);
    }

    [Fact]
    public void ReserveStock_ThrowsWhenStockIsInsufficient()
    {
        var product = Product.Create("Monitor", "4K 144Hz", 399.99m, 3);

        Assert.Throws<InsufficientStockException>(() => product.ReserveStock(Guid.NewGuid(), 4));
    }

    [Fact]
    public void ConfirmReservation_DeductsBothStockAndReservedQuantity()
    {
        var product = Product.Create("Laptop", "16GB RAM", 999.99m, 10);
        var reservation = product.ReserveStock(Guid.NewGuid(), 3);

        product.ConfirmReservation(reservation.Quantity);
        reservation.Confirm();

        Assert.Equal(7, product.StockQuantity);
        Assert.Equal(0, product.ReservedQuantity);
        Assert.Equal(7, product.AvailableQuantity);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
    }

    [Fact]
    public void ReleaseReservation_DeductsReservedQuantity()
    {
        var product = Product.Create("Laptop", "16GB RAM", 999.99m, 10);
        var reservation = product.ReserveStock(Guid.NewGuid(), 3);

        product.ReleaseReservation(reservation.Quantity);
        reservation.Release();

        Assert.Equal(10, product.StockQuantity);
        Assert.Equal(0, product.ReservedQuantity);
        Assert.Equal(10, product.AvailableQuantity);
        Assert.Equal(ReservationStatus.Released, reservation.Status);
    }

    [Fact]
    public void Reservation_ThrowsWhenConfirmingAlreadyConfirmed()
    {
        var product = Product.Create("Headphones", "ANC", 199.99m, 5);
        var reservation = product.ReserveStock(Guid.NewGuid(), 2);
        reservation.Confirm();

        Assert.Throws<InvalidReservationStateException>(() => reservation.Confirm());
    }
}
