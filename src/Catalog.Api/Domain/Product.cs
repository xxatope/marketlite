namespace Catalog.Api.Domain;

public sealed class Product
{
    private Product()
    {
    }

    private Product(string name, string description, decimal price, int initialStock)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        Price = price;
        StockQuantity = initialStock;
        ReservedQuantity = 0;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int StockQuantity { get; private set; }
    public int ReservedQuantity { get; private set; }
    public int AvailableQuantity => StockQuantity - ReservedQuantity;
    public DateTime CreatedAt { get; private set; }

    public static Product Create(string name, string description, decimal price, int initialStock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (price <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be strictly positive.");
        }

        if (initialStock < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialStock), "Initial stock quantity cannot be negative.");
        }

        return new Product(name.Trim(), description?.Trim() ?? string.Empty, price, initialStock);
    }

    public void AddStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Restock quantity must be strictly positive.");
        }

        StockQuantity += quantity;
    }

    public StockReservation ReserveStock(Guid orderId, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Reservation quantity must be strictly positive.");
        }

        if (AvailableQuantity < quantity)
        {
            throw new InsufficientStockException(Id, quantity, AvailableQuantity);
        }

        ReservedQuantity += quantity;

        return new StockReservation(orderId, Id, quantity);
    }

    public void ConfirmReservation(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Confirmed quantity must be strictly positive.");
        }

        if (ReservedQuantity < quantity || StockQuantity < quantity)
        {
            throw new InvalidOperationException($"Cannot confirm reservation of {quantity} when reserved is {ReservedQuantity} and stock is {StockQuantity}.");
        }

        StockQuantity -= quantity;
        ReservedQuantity -= quantity;
    }

    public void ReleaseReservation(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Released quantity must be strictly positive.");
        }

        if (ReservedQuantity < quantity)
        {
            throw new InvalidOperationException($"Cannot release reservation of {quantity} when reserved is only {ReservedQuantity}.");
        }

        ReservedQuantity -= quantity;
    }
}
