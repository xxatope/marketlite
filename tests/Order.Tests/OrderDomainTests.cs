using Order.Api.Domain;
using OrderEntity = Order.Api.Domain.Order;

namespace Order.Tests;

public sealed class OrderDomainTests
{
    [Fact]
    public void Create_CalculatesTotalAmount_AndInitializesPendingState()
    {
        var customerId = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var items = new List<(Guid, string, int, decimal)>
        {
            (p1, "Keyboard", 2, 100m),
            (p2, "Mouse", 1, 50.50m)
        };

        var order = OrderEntity.Create(customerId, "RUB", items, TimeProvider.System);

        Assert.Equal(customerId, order.CustomerId);
        Assert.Equal("RUB", order.Currency);
        Assert.Equal(250.50m, order.TotalAmount);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(2, order.Items.Count);
    }

    [Fact]
    public void Create_ThrowsWhenItemsAreEmpty()
    {
        Assert.Throws<EmptyOrderException>(() =>
            OrderEntity.Create(Guid.NewGuid(), "USD", Enumerable.Empty<(Guid, string, int, decimal)>(), TimeProvider.System));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(1, 0)]
    [InlineData(1, -10)]
    public void Create_ThrowsWhenQuantityOrPriceIsInvalid(int quantity, decimal price)
    {
        var items = new List<(Guid, string, int, decimal)>
        {
            (Guid.NewGuid(), "Item", quantity, price)
        };

        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            OrderEntity.Create(Guid.NewGuid(), "USD", items, TimeProvider.System));
    }

    [Fact]
    public void Confirm_TransitionsToConfirmed()
    {
        var items = new List<(Guid, string, int, decimal)> { (Guid.NewGuid(), "Item", 1, 50m) };
        var order = OrderEntity.Create(Guid.NewGuid(), "RUB", items, TimeProvider.System);

        order.Confirm(TimeProvider.System);

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.NotNull(order.ConfirmedAt);
    }

    [Fact]
    public void Cancel_TransitionsToCancelled()
    {
        var items = new List<(Guid, string, int, decimal)> { (Guid.NewGuid(), "Item", 1, 50m) };
        var order = OrderEntity.Create(Guid.NewGuid(), "RUB", items, TimeProvider.System);

        order.Cancel("Out of stock", TimeProvider.System);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.NotNull(order.CancelledAt);
        Assert.Equal("Out of stock", order.CancellationReason);
    }

    [Fact]
    public void Confirm_ThrowsWhenAlreadyConfirmed()
    {
        var items = new List<(Guid, string, int, decimal)> { (Guid.NewGuid(), "Item", 1, 50m) };
        var order = OrderEntity.Create(Guid.NewGuid(), "RUB", items, TimeProvider.System);
        order.Confirm(TimeProvider.System);

        Assert.Throws<InvalidOrderStateException>(() => order.Confirm(TimeProvider.System));
    }
}
