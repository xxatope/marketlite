namespace Order.Api.Domain;

public sealed class Order
{
    private readonly List<OrderItem> _items = new();

    private Order()
    {
    }

    private Order(Guid customerId, string currency, DateTime createdAt)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        Currency = currency;
        Status = OrderStatus.Pending;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public decimal TotalAmount { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public uint Version { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public static Order Create(
        Guid customerId,
        string currency,
        IEnumerable<(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)> items,
        TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        var itemList = items?.ToList() ?? throw new EmptyOrderException();

        if (itemList.Count == 0)
        {
            throw new EmptyOrderException();
        }

        var order = new Order(customerId, currency.Trim().ToUpperInvariant(), timeProvider.GetUtcNow().UtcDateTime);

        foreach (var item in itemList)
        {
            if (item.Quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(items), "Item quantity must be strictly positive.");
            }

            if (item.UnitPrice <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(items), "Unit price must be strictly positive.");
            }

            if (decimal.Round(item.UnitPrice, 2) != item.UnitPrice)
            {
                throw new ArgumentException($"Unit price '{item.UnitPrice}' cannot have more than 2 decimal places.", nameof(items));
            }

            var orderItem = new OrderItem(order.Id, item.ProductId, item.ProductName, item.Quantity, item.UnitPrice);
            order._items.Add(orderItem);
        }

        order.TotalAmount = order._items.Sum(i => i.Subtotal);

        return order;
    }

    public void Confirm(TimeProvider timeProvider)
    {
        if (Status != OrderStatus.Pending)
        {
            throw new InvalidOrderStateException(Id, Status, "confirm");
        }

        Status = OrderStatus.Confirmed;
        ConfirmedAt = timeProvider.GetUtcNow().UtcDateTime;
    }

    public void Cancel(string reason, TimeProvider timeProvider)
    {
        if (Status != OrderStatus.Pending)
        {
            throw new InvalidOrderStateException(Id, Status, "cancel");
        }

        Status = OrderStatus.Cancelled;
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by system" : reason;
        CancelledAt = timeProvider.GetUtcNow().UtcDateTime;
    }
}
